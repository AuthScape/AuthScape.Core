using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuthScape.Models.PaymentGateway;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Context;
using Services.Database;
using Stripe;

namespace AuthScape.Services.PromoCode
{
    public class PromoCodeService : IPromoCodeService
    {
        private readonly DatabaseContext _context;
        private readonly AppSettings _appSettings;
        private static readonly Random _random = new Random();
        private const string CodeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public PromoCodeService(
            DatabaseContext context,
            IOptions<AppSettings> appSettings)
        {
            _context = context;
            _appSettings = appSettings.Value;

            // Set Stripe API key
            StripeConfiguration.ApiKey = _appSettings.Stripe?.SecretKey;
        }

        #region CRUD Operations

        public async Task<List<PromoCodeDto>> GetAllAsync(bool includeInactive = false)
        {
            var query = _context.PromoCodes.AsQueryable();

            if (!includeInactive)
                query = query.Where(p => p.IsActive);

            var promoCodes = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var dtos = new List<PromoCodeDto>();
            foreach (var promoCode in promoCodes)
            {
                dtos.Add(await MapToDtoAsync(promoCode));
            }
            return dtos;
        }

        public async Task<PromoCodeDto?> GetByIdAsync(Guid id)
        {
            var promoCode = await _context.PromoCodes
                .FirstOrDefaultAsync(p => p.Id == id);

            return promoCode != null ? await MapToDtoAsync(promoCode) : null;
        }

        public async Task<PromoCodeDto?> GetByCodeAsync(string code)
        {
            var promoCode = await _context.PromoCodes
                .FirstOrDefaultAsync(p => p.Code.ToUpper() == code.ToUpper());

            return promoCode != null ? await MapToDtoAsync(promoCode) : null;
        }

        public async Task<Guid> CreateAsync(CreatePromoCodeDto dto)
        {
            // Generate code if auto-generate is enabled or code is empty
            var code = dto.AutoGenerateCode || string.IsNullOrWhiteSpace(dto.Code)
                ? GenerateRandomCode()
                : dto.Code!.ToUpper().Trim();

            // Check if code already exists
            var existingCode = await _context.PromoCodes.AnyAsync(p => p.Code == code);
            if (existingCode)
            {
                throw new InvalidOperationException($"Promo code '{code}' already exists.");
            }

            var promoCode = new AuthScape.Models.PaymentGateway.PromoCode
            {
                Name = dto.Name,
                Description = dto.Description,
                Code = code,
                DiscountType = dto.DiscountType,
                DiscountValue = dto.DiscountValue,
                Currency = dto.Currency,
                Duration = dto.Duration,
                DurationInMonths = dto.DurationInMonths,
                MaxRedemptions = dto.MaxRedemptions,
                MaxRedemptionsPerCustomer = dto.MaxRedemptionsPerCustomer,
                TimesRedeemed = 0,
                StartsAt = dto.StartsAt,
                ExpiresAt = dto.ExpiresAt,
                Scope = dto.Scope,
                RestrictedToUserId = dto.Scope == PromoCodeScope.User ? dto.RestrictedToUserId : null,
                RestrictedToCompanyId = dto.Scope == PromoCodeScope.Company ? dto.RestrictedToCompanyId : null,
                RestrictedToLocationId = dto.Scope == PromoCodeScope.Location ? dto.RestrictedToLocationId : null,
                AppliesTo = dto.AppliesTo,
                ApplicablePlanIds = dto.ApplicablePlanIds?.Any() == true
                    ? string.Join(",", dto.ApplicablePlanIds)
                    : null,
                ApplicableProductIds = dto.ApplicableProductIds?.Any() == true
                    ? string.Join(",", dto.ApplicableProductIds)
                    : null,
                ExtendsTrialDays = dto.ExtendsTrialDays,
                AdditionalTrialDays = dto.AdditionalTrialDays,
                MinimumAmount = dto.MinimumAmount,
                IsActive = dto.IsActive,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = dto.CreatedByUserId
            };

            _context.PromoCodes.Add(promoCode);
            await _context.SaveChangesAsync();

            // Sync to Stripe (always sync per user requirement)
            await SyncToStripeAsync(promoCode.Id);

            return promoCode.Id;
        }

        public async Task<bool> UpdateAsync(UpdatePromoCodeDto dto)
        {
            var promoCode = await _context.PromoCodes.FindAsync(dto.Id);
            if (promoCode == null) return false;

            // Check if code changed and if new code already exists
            var newCode = dto.Code?.ToUpper().Trim() ?? promoCode.Code;
            if (newCode != promoCode.Code)
            {
                var existingCode = await _context.PromoCodes.AnyAsync(p => p.Code == newCode && p.Id != dto.Id);
                if (existingCode)
                {
                    throw new InvalidOperationException($"Promo code '{newCode}' already exists.");
                }
            }

            promoCode.Name = dto.Name;
            promoCode.Description = dto.Description;
            promoCode.Code = newCode;
            promoCode.DiscountType = dto.DiscountType;
            promoCode.DiscountValue = dto.DiscountValue;
            promoCode.Currency = dto.Currency;
            promoCode.Duration = dto.Duration;
            promoCode.DurationInMonths = dto.DurationInMonths;
            promoCode.MaxRedemptions = dto.MaxRedemptions;
            promoCode.MaxRedemptionsPerCustomer = dto.MaxRedemptionsPerCustomer;
            promoCode.StartsAt = dto.StartsAt;
            promoCode.ExpiresAt = dto.ExpiresAt;
            promoCode.Scope = dto.Scope;
            promoCode.RestrictedToUserId = dto.Scope == PromoCodeScope.User ? dto.RestrictedToUserId : null;
            promoCode.RestrictedToCompanyId = dto.Scope == PromoCodeScope.Company ? dto.RestrictedToCompanyId : null;
            promoCode.RestrictedToLocationId = dto.Scope == PromoCodeScope.Location ? dto.RestrictedToLocationId : null;
            promoCode.AppliesTo = dto.AppliesTo;
            promoCode.ApplicablePlanIds = dto.ApplicablePlanIds?.Any() == true
                ? string.Join(",", dto.ApplicablePlanIds)
                : null;
            promoCode.ApplicableProductIds = dto.ApplicableProductIds?.Any() == true
                ? string.Join(",", dto.ApplicableProductIds)
                : null;
            promoCode.ExtendsTrialDays = dto.ExtendsTrialDays;
            promoCode.AdditionalTrialDays = dto.AdditionalTrialDays;
            promoCode.MinimumAmount = dto.MinimumAmount;
            promoCode.IsActive = dto.IsActive;
            promoCode.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();

            // Re-sync to Stripe if needed
            await SyncToStripeAsync(promoCode.Id);

            return true;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var promoCode = await _context.PromoCodes.FindAsync(id);
            if (promoCode == null) return false;

            // Deactivate in Stripe if synced
            if (!string.IsNullOrEmpty(promoCode.StripePromotionCodeId))
            {
                try
                {
                    var promoService = new PromotionCodeService();
                    await promoService.UpdateAsync(promoCode.StripePromotionCodeId, new PromotionCodeUpdateOptions
                    {
                        Active = false
                    });
                }
                catch
                {
                    // Continue with delete even if Stripe update fails
                }
            }

            _context.PromoCodes.Remove(promoCode);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ToggleActiveAsync(Guid id)
        {
            var promoCode = await _context.PromoCodes.FindAsync(id);
            if (promoCode == null) return false;

            promoCode.IsActive = !promoCode.IsActive;
            promoCode.UpdatedAt = DateTimeOffset.UtcNow;

            // Update Stripe promotion code status
            if (!string.IsNullOrEmpty(promoCode.StripePromotionCodeId))
            {
                try
                {
                    var promoService = new PromotionCodeService();
                    await promoService.UpdateAsync(promoCode.StripePromotionCodeId, new PromotionCodeUpdateOptions
                    {
                        Active = promoCode.IsActive
                    });
                }
                catch
                {
                    // Continue even if Stripe update fails
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }

        #endregion

        #region Code Generation

        public string GenerateRandomCode(int length = 8)
        {
            var code = new char[length];
            for (int i = 0; i < length; i++)
            {
                code[i] = CodeCharacters[_random.Next(CodeCharacters.Length)];
            }
            return new string(code);
        }

        #endregion

        #region Validation

        public async Task<PromoCodeValidationResult> ValidateCodeAsync(
            string code,
            Guid? planId = null,
            long? userId = null,
            long? companyId = null,
            long? locationId = null,
            decimal? orderAmount = null)
        {
            var promoCode = await _context.PromoCodes
                .FirstOrDefaultAsync(p => p.Code.ToUpper() == code.ToUpper());

            if (promoCode == null)
                return PromoCodeValidationResult.Invalid("Invalid promo code.");

            // Check if active
            if (!promoCode.IsActive)
                return PromoCodeValidationResult.Invalid("This promo code is no longer active.");

            // Check validity dates
            var now = DateTimeOffset.UtcNow;
            if (promoCode.StartsAt.HasValue && now < promoCode.StartsAt.Value)
                return PromoCodeValidationResult.Invalid("This promo code is not yet active.");

            if (promoCode.ExpiresAt.HasValue && now > promoCode.ExpiresAt.Value)
                return PromoCodeValidationResult.Invalid("This promo code has expired.");

            // Check max redemptions
            if (promoCode.MaxRedemptions.HasValue && promoCode.TimesRedeemed >= promoCode.MaxRedemptions.Value)
                return PromoCodeValidationResult.Invalid("This promo code has reached its maximum redemptions.");

            // Check scope restrictions
            switch (promoCode.Scope)
            {
                case PromoCodeScope.User:
                    if (!userId.HasValue || promoCode.RestrictedToUserId != userId.Value)
                        return PromoCodeValidationResult.Invalid("This promo code is not available for your account.");
                    break;

                case PromoCodeScope.Company:
                    if (!companyId.HasValue || promoCode.RestrictedToCompanyId != companyId.Value)
                        return PromoCodeValidationResult.Invalid("This promo code is not available for your company.");
                    break;

                case PromoCodeScope.Location:
                    if (!locationId.HasValue || promoCode.RestrictedToLocationId != locationId.Value)
                        return PromoCodeValidationResult.Invalid("This promo code is not available for your location.");
                    break;
            }

            // Check plan applicability
            if (planId.HasValue && promoCode.AppliesTo != PromoCodeAppliesTo.All)
            {
                if (promoCode.AppliesTo == PromoCodeAppliesTo.Products || promoCode.AppliesTo == PromoCodeAppliesTo.Services)
                    return PromoCodeValidationResult.Invalid("This promo code cannot be applied to subscriptions.");

                if (!string.IsNullOrEmpty(promoCode.ApplicablePlanIds))
                {
                    var applicablePlanIds = promoCode.ApplicablePlanIdList;
                    if (applicablePlanIds.Any() && !applicablePlanIds.Contains(planId.Value))
                        return PromoCodeValidationResult.Invalid("This promo code cannot be applied to the selected plan.");
                }
            }

            // Check minimum amount
            if (promoCode.MinimumAmount.HasValue && orderAmount.HasValue && orderAmount.Value < promoCode.MinimumAmount.Value)
                return PromoCodeValidationResult.Invalid($"Minimum order amount of ${promoCode.MinimumAmount.Value:F2} required.");

            // Check Stripe sync
            if (string.IsNullOrEmpty(promoCode.StripePromotionCodeId))
                return PromoCodeValidationResult.Invalid("This promo code is not properly configured. Please contact support.");

            return PromoCodeValidationResult.Valid(await MapToDtoAsync(promoCode));
        }

        #endregion

        #region Stripe Sync

        public async Task<bool> SyncToStripeAsync(Guid promoCodeId)
        {
            var promoCode = await _context.PromoCodes.FindAsync(promoCodeId);
            if (promoCode == null) return false;

            try
            {
                // Step 1: Create or update Stripe Coupon
                string couponId;
                if (string.IsNullOrEmpty(promoCode.StripeCouponId))
                {
                    // Create new coupon
                    var couponService = new CouponService();
                    var couponOptions = new CouponCreateOptions
                    {
                        Name = promoCode.Name,
                        Duration = MapDurationToStripe(promoCode.Duration),
                        DurationInMonths = promoCode.Duration == PromoDuration.Repeating ? promoCode.DurationInMonths : null,
                        MaxRedemptions = promoCode.MaxRedemptions,
                        RedeemBy = promoCode.ExpiresAt?.UtcDateTime
                    };

                    // Set discount
                    if (promoCode.DiscountType == PromoCodeType.Percentage)
                    {
                        couponOptions.PercentOff = promoCode.DiscountValue;
                    }
                    else
                    {
                        couponOptions.AmountOff = (long)(promoCode.DiscountValue * 100);
                        couponOptions.Currency = promoCode.Currency;
                    }

                    var coupon = await couponService.CreateAsync(couponOptions);
                    couponId = coupon.Id;
                    promoCode.StripeCouponId = couponId;
                }
                else
                {
                    couponId = promoCode.StripeCouponId;
                    // Note: Stripe coupons have limited update capabilities after creation
                    // Most properties cannot be changed - would need to create a new coupon
                }

                // Step 2: Create or update Stripe Promotion Code
                if (string.IsNullOrEmpty(promoCode.StripePromotionCodeId))
                {
                    var promoService = new PromotionCodeService();
                    var promoOptions = new PromotionCodeCreateOptions
                    {
                        Promotion = new PromotionCodePromotionOptions
                        {
                            Coupon = couponId
                        },
                        Code = promoCode.Code,
                        Active = promoCode.IsActive,
                        MaxRedemptions = promoCode.MaxRedemptions,
                        ExpiresAt = promoCode.ExpiresAt?.UtcDateTime
                    };

                    // Add restrictions
                    if (promoCode.MinimumAmount.HasValue)
                    {
                        promoOptions.Restrictions = new PromotionCodeRestrictionsOptions
                        {
                            MinimumAmount = (long)(promoCode.MinimumAmount.Value * 100),
                            MinimumAmountCurrency = promoCode.Currency
                        };
                    }

                    var stripePromo = await promoService.CreateAsync(promoOptions);
                    promoCode.StripePromotionCodeId = stripePromo.Id;
                }
                else
                {
                    // Update existing promotion code (limited options available)
                    var promoService = new PromotionCodeService();
                    await promoService.UpdateAsync(promoCode.StripePromotionCodeId, new PromotionCodeUpdateOptions
                    {
                        Active = promoCode.IsActive
                    });
                }

                promoCode.LastStripeSyncAt = DateTimeOffset.UtcNow;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (StripeException ex)
            {
                throw new InvalidOperationException($"Stripe sync failed: {ex.Message}", ex);
            }
        }

        public async Task<int> SyncAllFromStripeAsync()
        {
            var promoService = new PromotionCodeService();
            var options = new PromotionCodeListOptions
            {
                Active = true,
                Limit = 100,
                Expand = new List<string> { "data.coupon" }
            };

            var stripePromos = await promoService.ListAsync(options);
            int syncCount = 0;

            foreach (var stripePromo in stripePromos.Data)
            {
                var existing = await _context.PromoCodes
                    .FirstOrDefaultAsync(p => p.StripePromotionCodeId == stripePromo.Id);

                if (existing != null)
                {
                    // Update existing
                    existing.LastStripeSyncAt = DateTimeOffset.UtcNow;
                    existing.IsActive = stripePromo.Active;
                }
                else
                {
                    // Create new from Stripe - access coupon via Promotion.CouponId
                    var couponService = new CouponService();
                    Coupon? coupon = null;
                    var couponId = stripePromo.Promotion?.CouponId;
                    if (!string.IsNullOrEmpty(couponId))
                    {
                        try
                        {
                            coupon = await couponService.GetAsync(couponId);
                        }
                        catch
                        {
                            // Coupon may have been deleted, continue without it
                        }
                    }

                    var newPromo = new AuthScape.Models.PaymentGateway.PromoCode
                    {
                        Name = coupon?.Name ?? stripePromo.Code,
                        Code = stripePromo.Code,
                        StripeCouponId = coupon?.Id,
                        StripePromotionCodeId = stripePromo.Id,
                        DiscountType = coupon?.PercentOff.HasValue == true ? PromoCodeType.Percentage : PromoCodeType.FixedAmount,
                        DiscountValue = coupon?.PercentOff ?? (coupon?.AmountOff.HasValue == true ? coupon.AmountOff.Value / 100m : 0),
                        Currency = coupon?.Currency ?? "usd",
                        Duration = MapStripeDuration(coupon?.Duration),
                        DurationInMonths = (int?)(coupon?.DurationInMonths),
                        MaxRedemptions = (int?)(stripePromo.MaxRedemptions),
                        TimesRedeemed = (int)(stripePromo.TimesRedeemed),
                        ExpiresAt = stripePromo.ExpiresAt.HasValue ? new DateTimeOffset(stripePromo.ExpiresAt.Value) : null,
                        IsActive = stripePromo.Active,
                        Scope = PromoCodeScope.All,
                        AppliesTo = PromoCodeAppliesTo.All,
                        LastStripeSyncAt = DateTimeOffset.UtcNow,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    _context.PromoCodes.Add(newPromo);
                }
                syncCount++;
            }

            await _context.SaveChangesAsync();
            return syncCount;
        }

        #endregion

        #region Usage Tracking

        public async Task<bool> RecordRedemptionAsync(Guid promoCodeId)
        {
            var promoCode = await _context.PromoCodes.FindAsync(promoCodeId);
            if (promoCode == null) return false;

            promoCode.TimesRedeemed++;
            promoCode.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        #endregion

        #region Search Methods

        public async Task<List<UserSearchResult>> SearchUsersAsync(string searchTerm, int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<UserSearchResult>();

            var term = searchTerm.ToLower();
            return await _context.Users
                .Where(u => u.FirstName!.ToLower().Contains(term) ||
                           u.LastName!.ToLower().Contains(term) ||
                           u.Email!.ToLower().Contains(term))
                .Take(limit)
                .Select(u => new UserSearchResult
                {
                    Id = u.Id,
                    DisplayName = $"{u.FirstName} {u.LastName}".Trim(),
                    Email = u.Email
                })
                .ToListAsync();
        }

        public async Task<List<CompanySearchResult>> SearchCompaniesAsync(string searchTerm, int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<CompanySearchResult>();

            var term = searchTerm.ToLower();
            return await _context.Companies
                .Where(c => c.Title.ToLower().Contains(term))
                .Take(limit)
                .Select(c => new CompanySearchResult
                {
                    Id = c.Id,
                    Name = c.Title
                })
                .ToListAsync();
        }

        public async Task<List<LocationSearchResult>> SearchLocationsAsync(string searchTerm, int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<LocationSearchResult>();

            var term = searchTerm.ToLower();
            return await _context.Locations
                .Include(l => l.Company)
                .Where(l => l.Title.ToLower().Contains(term))
                .Take(limit)
                .Select(l => new LocationSearchResult
                {
                    Id = l.Id,
                    Name = l.Title,
                    CompanyId = l.CompanyId,
                    CompanyName = l.Company != null ? l.Company.Title : null
                })
                .ToListAsync();
        }

        #endregion

        #region Helper Methods

        private async Task<PromoCodeDto> MapToDtoAsync(AuthScape.Models.PaymentGateway.PromoCode promoCode)
        {
            // Lookup related entity names
            string? restrictedToUserName = null;
            string? restrictedToCompanyName = null;
            string? restrictedToLocationName = null;

            if (promoCode.RestrictedToUserId.HasValue)
            {
                var user = await _context.Users.FindAsync(promoCode.RestrictedToUserId.Value);
                if (user != null)
                    restrictedToUserName = $"{user.FirstName} {user.LastName}".Trim();
            }

            if (promoCode.RestrictedToCompanyId.HasValue)
            {
                var company = await _context.Companies.FindAsync(promoCode.RestrictedToCompanyId.Value);
                if (company != null)
                    restrictedToCompanyName = company.Title;
            }

            if (promoCode.RestrictedToLocationId.HasValue)
            {
                var location = await _context.Locations.FindAsync(promoCode.RestrictedToLocationId.Value);
                if (location != null)
                    restrictedToLocationName = location.Title;
            }

            return new PromoCodeDto
            {
                Id = promoCode.Id,
                Name = promoCode.Name,
                Description = promoCode.Description,
                Code = promoCode.Code,
                StripeCouponId = promoCode.StripeCouponId,
                StripePromotionCodeId = promoCode.StripePromotionCodeId,
                LastStripeSyncAt = promoCode.LastStripeSyncAt,
                DiscountType = promoCode.DiscountType,
                DiscountValue = promoCode.DiscountValue,
                Currency = promoCode.Currency,
                Duration = promoCode.Duration,
                DurationInMonths = promoCode.DurationInMonths,
                MaxRedemptions = promoCode.MaxRedemptions,
                TimesRedeemed = promoCode.TimesRedeemed,
                MaxRedemptionsPerCustomer = promoCode.MaxRedemptionsPerCustomer,
                StartsAt = promoCode.StartsAt,
                ExpiresAt = promoCode.ExpiresAt,
                Scope = promoCode.Scope,
                RestrictedToUserId = promoCode.RestrictedToUserId,
                RestrictedToUserName = restrictedToUserName,
                RestrictedToCompanyId = promoCode.RestrictedToCompanyId,
                RestrictedToCompanyName = restrictedToCompanyName,
                RestrictedToLocationId = promoCode.RestrictedToLocationId,
                RestrictedToLocationName = restrictedToLocationName,
                AppliesTo = promoCode.AppliesTo,
                ApplicablePlanIds = promoCode.ApplicablePlanIds,
                ApplicableProductIds = promoCode.ApplicableProductIds,
                ExtendsTrialDays = promoCode.ExtendsTrialDays,
                AdditionalTrialDays = promoCode.AdditionalTrialDays,
                MinimumAmount = promoCode.MinimumAmount,
                IsActive = promoCode.IsActive,
                CreatedAt = promoCode.CreatedAt,
                UpdatedAt = promoCode.UpdatedAt,
                CreatedByUserId = promoCode.CreatedByUserId
            };
        }

        private static string MapDurationToStripe(PromoDuration duration)
        {
            return duration switch
            {
                PromoDuration.Once => "once",
                PromoDuration.Repeating => "repeating",
                PromoDuration.Forever => "forever",
                _ => "once"
            };
        }

        private static PromoDuration MapStripeDuration(string? duration)
        {
            return duration?.ToLower() switch
            {
                "once" => PromoDuration.Once,
                "repeating" => PromoDuration.Repeating,
                "forever" => PromoDuration.Forever,
                _ => PromoDuration.Once
            };
        }

        #endregion
    }
}
