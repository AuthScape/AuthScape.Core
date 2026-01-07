namespace AuthScape.MCP;

/// <summary>
/// Static component schema definitions for all 100 Puck components.
/// </summary>
public static class ComponentSchemas
{
    private static readonly Dictionary<string, ComponentInfo> Components = new()
    {
        // Layout Components
        ["Section"] = new("Section", "Layout", "Container section with background and padding options",
            new[] { "backgroundColor", "backgroundImage", "padding", "maxWidth", "minHeight" }),
        ["Row"] = new("Row", "Layout", "Horizontal row container for columns",
            new[] { "columns", "gap", "alignItems", "justifyContent", "wrap" }),
        ["Spacer"] = new("Spacer", "Layout", "Vertical spacing element",
            new[] { "height", "mobileHeight" }),
        ["Grid"] = new("Grid", "Layout", "MUI Grid2-based flexible grid layout",
            new[] { "columns", "spacing", "rowSpacing", "columnSpacing", "alignItems", "justifyContent" }),
        ["Stack"] = new("Stack", "Layout", "MUI Stack for vertical/horizontal stacking",
            new[] { "direction", "spacing", "alignItems", "justifyContent", "divider" }),
        ["FlexBox"] = new("FlexBox", "Layout", "CSS Flexbox container with full control",
            new[] { "flexDirection", "flexWrap", "justifyContent", "alignItems", "gap" }),

        // Hero Components
        ["HeroBasic"] = new("HeroBasic", "Heroes", "Simple hero with title, subtitle, and CTA",
            new[] { "title", "subtitle", "ctaText", "ctaLink", "backgroundColor", "textColor", "backgroundImage" }),
        ["HeroSplit"] = new("HeroSplit", "Heroes", "Two-column hero with image and content",
            new[] { "title", "subtitle", "ctaText", "ctaLink", "image", "imagePosition", "backgroundColor" }),
        ["HeroVideo"] = new("HeroVideo", "Heroes", "Hero with video background",
            new[] { "title", "subtitle", "videoUrl", "overlayColor", "overlayOpacity" }),
        ["HeroSlider"] = new("HeroSlider", "Heroes", "Carousel hero with multiple slides",
            new[] { "slides", "autoplay", "interval", "showDots", "showArrows" }),
        ["HeroImage"] = new("HeroImage", "Heroes", "Full-width image hero",
            new[] { "title", "subtitle", "backgroundImage", "overlayColor", "height" }),
        ["HeroAnimated"] = new("HeroAnimated", "Heroes", "Hero with animated elements",
            new[] { "title", "subtitle", "animation", "animationDelay" }),

        // Card Components
        ["Card"] = new("Card", "Cards", "Basic card with image, title, and description",
            new[] { "title", "description", "image", "link", "elevation" }),
        ["FeatureCard"] = new("FeatureCard", "Cards", "Feature highlight card with icon",
            new[] { "title", "description", "icon", "iconColor", "backgroundColor" }),
        ["PricingCard"] = new("PricingCard", "Cards", "Pricing plan card",
            new[] { "title", "price", "period", "features", "ctaText", "ctaLink", "highlighted" }),
        ["TestimonialCard"] = new("TestimonialCard", "Cards", "Customer testimonial card",
            new[] { "quote", "author", "role", "company", "avatar", "rating" }),
        ["TeamCard"] = new("TeamCard", "Cards", "Team member profile card",
            new[] { "name", "role", "image", "bio", "socialLinks" }),
        ["BlogCard"] = new("BlogCard", "Cards", "Blog post preview card",
            new[] { "title", "excerpt", "image", "author", "date", "category", "link" }),
        ["ProductCard"] = new("ProductCard", "Cards", "E-commerce product card",
            new[] { "title", "price", "originalPrice", "image", "rating", "badge" }),
        ["ServiceCard"] = new("ServiceCard", "Cards", "Service offering card",
            new[] { "title", "description", "icon", "features", "ctaText" }),
        ["PortfolioCard"] = new("PortfolioCard", "Cards", "Portfolio/project showcase card",
            new[] { "title", "category", "image", "description", "link" }),
        ["ComparisonCard"] = new("ComparisonCard", "Cards", "Before/after comparison card",
            new[] { "title", "beforeImage", "afterImage", "description" }),

        // Media Components
        ["Video"] = new("Video", "Media", "Video player component",
            new[] { "src", "poster", "autoplay", "controls", "loop", "muted" }),
        ["Gallery"] = new("Gallery", "Media", "Image gallery with lightbox",
            new[] { "images", "columns", "gap", "lightbox", "captions" }),
        ["Carousel"] = new("Carousel", "Media", "Image/content carousel",
            new[] { "slides", "autoplay", "interval", "showDots", "showArrows" }),
        ["ImageCompare"] = new("ImageCompare", "Media", "Before/after image comparison slider",
            new[] { "beforeImage", "afterImage", "orientation", "initialPosition" }),
        ["Icon"] = new("Icon", "Media", "Material icon display",
            new[] { "icon", "size", "color" }),
        ["Avatar"] = new("Avatar", "Media", "User avatar image",
            new[] { "src", "alt", "size", "variant" }),
        ["AudioPlayer"] = new("AudioPlayer", "Media", "Audio file player",
            new[] { "src", "title", "artist", "coverImage", "showControls" }),
        ["LottieAnimation"] = new("LottieAnimation", "Media", "Lottie JSON animation player",
            new[] { "animationUrl", "loop", "autoplay", "speed", "width", "height" }),

        // Navigation Components
        ["Accordion"] = new("Accordion", "Navigation", "Expandable accordion panels",
            new[] { "items", "allowMultiple", "defaultExpanded" }),
        ["Tabs"] = new("Tabs", "Navigation", "Tabbed content panels",
            new[] { "tabs", "variant", "orientation", "centered" }),
        ["Breadcrumb"] = new("Breadcrumb", "Navigation", "Navigation breadcrumb trail",
            new[] { "items", "separator", "maxItems" }),
        ["StepIndicator"] = new("StepIndicator", "Navigation", "Multi-step progress indicator",
            new[] { "steps", "activeStep", "orientation", "variant" }),
        ["ScrollToTop"] = new("ScrollToTop", "Navigation", "Floating scroll-to-top button",
            new[] { "position", "showAfter", "smooth", "icon", "backgroundColor" }),
        ["TableOfContents"] = new("TableOfContents", "Navigation", "Auto-generated page section links",
            new[] { "title", "headingSelector", "sticky", "smooth", "highlightActive" }),

        // Content Components
        ["FAQ"] = new("FAQ", "Content", "Frequently asked questions accordion",
            new[] { "items", "columns", "searchable" }),
        ["Features"] = new("Features", "Content", "Feature grid display",
            new[] { "features", "columns", "iconPosition", "variant" }),
        ["Steps"] = new("Steps", "Content", "Process/steps visualization",
            new[] { "steps", "variant", "orientation", "connector" }),
        ["ContentBlock"] = new("ContentBlock", "Content", "Rich text content block",
            new[] { "content", "textAlign", "maxWidth" }),
        ["Testimonials"] = new("Testimonials", "Content", "Testimonial grid/slider display",
            new[] { "testimonials", "layout", "columns", "autoplay", "showRating" }),
        ["PricingTable"] = new("PricingTable", "Content", "Full pricing comparison table",
            new[] { "plans", "showToggle", "yearlyDiscount", "columns" }),
        ["Stats"] = new("Stats", "Content", "Statistics/metrics display",
            new[] { "stats", "columns", "animated", "variant" }),
        ["Timeline"] = new("Timeline", "Content", "Chronological timeline",
            new[] { "items", "orientation", "variant" }),

        // Typography Components
        ["Heading"] = new("Heading", "Typography", "Heading text (h1-h6)",
            new[] { "text", "level", "align", "color" }),
        ["Text"] = new("Text", "Typography", "Paragraph text",
            new[] { "content", "variant", "align", "color" }),
        ["Quote"] = new("Quote", "Typography", "Blockquote with citation",
            new[] { "quote", "author", "source", "variant" }),
        ["List"] = new("List", "Typography", "Bulleted or numbered list",
            new[] { "items", "variant", "icon" }),
        ["Code"] = new("Code", "Typography", "Code block with syntax highlighting",
            new[] { "code", "language", "showLineNumbers", "theme" }),
        ["Divider"] = new("Divider", "Typography", "Horizontal divider line",
            new[] { "variant", "text", "textAlign" }),

        // Form Components
        ["FormInput"] = new("FormInput", "Forms", "Text input field",
            new[] { "label", "placeholder", "type", "required", "helperText" }),
        ["FormTextArea"] = new("FormTextArea", "Forms", "Multi-line text input",
            new[] { "label", "placeholder", "rows", "required" }),
        ["FormSelect"] = new("FormSelect", "Forms", "Dropdown select field",
            new[] { "label", "options", "multiple", "required" }),
        ["FormCheckbox"] = new("FormCheckbox", "Forms", "Checkbox input",
            new[] { "label", "defaultChecked", "required" }),
        ["FormRadioGroup"] = new("FormRadioGroup", "Forms", "Radio button group",
            new[] { "label", "options", "defaultValue", "required" }),
        ["FormDatePicker"] = new("FormDatePicker", "Forms", "Date picker input",
            new[] { "label", "format", "minDate", "maxDate" }),
        ["FormFileUpload"] = new("FormFileUpload", "Forms", "File upload input",
            new[] { "label", "accept", "multiple", "maxSize" }),
        ["FormBuilder"] = new("FormBuilder", "Forms", "Dynamic form builder",
            new[] { "fields", "submitText", "action" }),

        // Button Components
        ["Button"] = new("Button", "Buttons", "Action button",
            new[] { "text", "variant", "color", "size", "link", "icon" }),
        ["ButtonGroup"] = new("ButtonGroup", "Buttons", "Group of buttons",
            new[] { "buttons", "variant", "orientation" }),
        ["IconButton"] = new("IconButton", "Buttons", "Icon-only button",
            new[] { "icon", "size", "color", "link" }),
        ["FloatingActionButton"] = new("FloatingActionButton", "Buttons", "Floating action button",
            new[] { "icon", "position", "color", "link" }),

        // E-Commerce Components
        ["Marketplace"] = new("Marketplace", "E-Commerce", "Embedded marketplace with search/filters",
            new[] { "platformId", "oemCompanyId", "pageSize", "cardGridSize", "showFilters" }),
        ["ShoppingCart"] = new("ShoppingCart", "E-Commerce", "Mini cart display widget",
            new[] { "showItemCount", "showTotal", "cartIcon", "emptyMessage" }),

        // Social Components
        ["SocialLinks"] = new("SocialLinks", "Social", "Social media icon links",
            new[] { "links", "variant", "size", "color" }),
        ["ShareButtons"] = new("ShareButtons", "Social", "Social sharing buttons",
            new[] { "platforms", "url", "title", "variant" }),
        ["SocialFeed"] = new("SocialFeed", "Social", "Social media feed embed",
            new[] { "platform", "username", "limit" }),

        // Embed Components
        ["IframeEmbed"] = new("IframeEmbed", "Embeds", "Generic iframe embed",
            new[] { "src", "width", "height", "title" }),
        ["YouTubeEmbed"] = new("YouTubeEmbed", "Embeds", "YouTube video embed",
            new[] { "videoId", "autoplay", "controls", "loop" }),
        ["MapEmbed"] = new("MapEmbed", "Embeds", "Google Maps embed",
            new[] { "address", "zoom", "height", "mapType" }),
        ["CalendarEmbed"] = new("CalendarEmbed", "Embeds", "Calendar booking embed",
            new[] { "calendarUrl", "height" }),
        ["FormEmbed"] = new("FormEmbed", "Embeds", "External form embed",
            new[] { "formUrl", "height" }),

        // CTA Components
        ["CTABanner"] = new("CTABanner", "CTA", "Call-to-action banner",
            new[] { "title", "subtitle", "ctaText", "ctaLink", "backgroundColor" }),
        ["CTASection"] = new("CTASection", "CTA", "Full-width CTA section",
            new[] { "title", "subtitle", "primaryCta", "secondaryCta", "backgroundImage" }),
        ["Newsletter"] = new("Newsletter", "CTA", "Newsletter signup form",
            new[] { "title", "subtitle", "placeholder", "buttonText", "successMessage" }),
        ["ContactForm"] = new("ContactForm", "CTA", "Contact form",
            new[] { "title", "fields", "submitText", "successMessage" }),

        // Banner Components
        ["AlertBanner"] = new("AlertBanner", "Banners", "Alert/notification banner",
            new[] { "message", "severity", "dismissible", "action" }),
        ["PromoBanner"] = new("PromoBanner", "Banners", "Promotional banner",
            new[] { "title", "subtitle", "ctaText", "ctaLink", "backgroundColor", "countdown" }),
        ["CookieBanner"] = new("CookieBanner", "Banners", "Cookie consent banner",
            new[] { "message", "acceptText", "declineText", "policyLink" }),

        // Logo Components
        ["Logo"] = new("Logo", "Logos", "Single logo display",
            new[] { "src", "alt", "width", "height", "link" }),
        ["LogoCloud"] = new("LogoCloud", "Logos", "Grid of partner/client logos",
            new[] { "logos", "columns", "grayscale", "title" }),
        ["LogoMarquee"] = new("LogoMarquee", "Logos", "Scrolling logo marquee",
            new[] { "logos", "speed", "direction", "pauseOnHover" }),

        // Counter Components
        ["Counter"] = new("Counter", "Counters", "Animated number counter",
            new[] { "value", "prefix", "suffix", "duration", "label" }),
        ["CountdownTimer"] = new("CountdownTimer", "Counters", "Countdown to date/time",
            new[] { "targetDate", "labels", "variant", "expiredMessage" }),

        // Progress Components
        ["ProgressBar"] = new("ProgressBar", "Progress", "Linear progress bar",
            new[] { "value", "label", "showValue", "color", "variant" }),
        ["ProgressCircle"] = new("ProgressCircle", "Progress", "Circular progress indicator",
            new[] { "value", "size", "thickness", "label" }),
        ["SkillBars"] = new("SkillBars", "Progress", "Multiple skill/progress bars",
            new[] { "skills", "animated", "variant" }),

        // Rating Components
        ["Rating"] = new("Rating", "Ratings", "Star rating display",
            new[] { "value", "max", "readOnly", "size" }),
        ["ReviewSummary"] = new("ReviewSummary", "Ratings", "Review score summary",
            new[] { "average", "total", "breakdown" }),

        // Misc Components
        ["Chip"] = new("Chip", "Misc", "Tag/chip component",
            new[] { "label", "color", "variant", "icon", "deletable" }),
        ["Badge"] = new("Badge", "Misc", "Badge/label component",
            new[] { "content", "color", "variant" }),
        ["Tooltip"] = new("Tooltip", "Misc", "Tooltip wrapper",
            new[] { "title", "placement", "arrow" }),
        ["Modal"] = new("Modal", "Misc", "Modal dialog trigger",
            new[] { "triggerText", "title", "content", "size" }),
        ["Drawer"] = new("Drawer", "Misc", "Side drawer panel",
            new[] { "triggerText", "anchor", "content", "width" }),
        ["Skeleton"] = new("Skeleton", "Misc", "Loading skeleton placeholder",
            new[] { "variant", "width", "height", "animation" }),
    };

    public static List<ComponentListItem> ListComponents(string? category = null)
    {
        var result = Components
            .Where(c => string.IsNullOrEmpty(category) ||
                        c.Value.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .Select(c => new ComponentListItem(c.Key, c.Value.Category, c.Value.Description))
            .OrderBy(c => c.Category)
            .ThenBy(c => c.Name)
            .ToList();

        return result;
    }

    public static ComponentSchema? GetSchema(string componentName)
    {
        if (Components.TryGetValue(componentName, out var info))
        {
            return new ComponentSchema(componentName, info.Category, info.Description, info.Fields);
        }
        return null;
    }

    public static List<string> GetCategories()
    {
        return Components.Values
            .Select(c => c.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }
}

public record ComponentInfo(string Name, string Category, string Description, string[] Fields);
public record ComponentListItem(string Name, string Category, string Description);
public record ComponentSchema(string Name, string Category, string Description, string[] Fields);
