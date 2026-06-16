/**
 * Unified Payments Core JavaScript
 * Manages tab navigation, Stripe integration, and API calls
 */

// ========================================
// Global State
// ========================================

const PaymentsApp = {
    // State
    stripe: null,
    elements: null,
    paymentElement: null,
    clientSecretCurrent: null,
    isMounting: false,
    isSubmitting: false,
    activeWalletId: null,
    stripePublicKey: null,
    anti: null,

    // Tab state
    tabsLoaded: {
        overview: false,
        subscriptions: false,
        'payment-methods': true, // Loaded server-side
        'billing-history': false,
        'quick-pay': false,
        'stripe-connect': true // Loaded server-side
    },

    // Subscription state
    selectedPlanId: null,
    selectedPlanGuid: null,
    appliedPromoCode: null,
    baseTrialDays: 0
};

// ========================================
// Initialization
// ========================================

document.addEventListener('DOMContentLoaded', function() {
    // Get config from page
    PaymentsApp.anti = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    // Extract wallet ID and Stripe key from the page (set by Razor)
    const walletIdEl = document.querySelector('[data-wallet-id]');
    const stripeKeyEl = document.querySelector('[data-stripe-key]');

    // Fallback: try to get from script tags or inline JS
    if (typeof activeWalletId !== 'undefined') {
        PaymentsApp.activeWalletId = activeWalletId;
    }
    if (typeof stripePublicKey !== 'undefined') {
        PaymentsApp.stripePublicKey = stripePublicKey;
    }

    // Initialize Stripe if key is available
    if (PaymentsApp.stripePublicKey) {
        PaymentsApp.stripe = Stripe(PaymentsApp.stripePublicKey);
    }

    // Set up tab navigation
    initTabNavigation();

    // Set up modal handlers
    initModals();

    // Set up payment method handlers
    initPaymentMethodHandlers();

    // Set up ACH verification
    initAchVerification();

    // Load initial data for visible tab
    const activeTab = document.querySelector('.payments-tab.active')?.dataset.tab || 'overview';
    loadTabData(activeTab);

    // Load payment methods for selects
    loadPaymentMethodsForSelects();

    // Auto-dismiss alerts after 5 seconds
    setTimeout(() => {
        document.getElementById('success-alert')?.remove();
        document.getElementById('error-alert')?.remove();
    }, 5000);
});

// ========================================
// Tab Navigation
// ========================================

function initTabNavigation() {
    document.querySelectorAll('.payments-tab').forEach(tab => {
        tab.addEventListener('click', function() {
            const tabName = this.dataset.tab;
            switchTab(tabName);
        });
    });

    // Handle URL tab parameter
    const urlParams = new URLSearchParams(window.location.search);
    const tabParam = urlParams.get('tab');
    if (tabParam) {
        switchTab(tabParam);
    }
}

function switchTab(tabName) {
    // Update tab buttons
    document.querySelectorAll('.payments-tab').forEach(tab => {
        tab.classList.toggle('active', tab.dataset.tab === tabName);
    });

    // Update tab panels
    document.querySelectorAll('.tab-panel').forEach(panel => {
        panel.style.display = panel.id === `tab-${tabName}` ? 'block' : 'none';
    });

    // Update URL without reload
    const url = new URL(window.location);
    url.searchParams.set('tab', tabName);
    window.history.replaceState({}, '', url);

    // Load tab data if not already loaded
    loadTabData(tabName);
}

function loadTabData(tabName) {
    if (PaymentsApp.tabsLoaded[tabName]) return;

    switch (tabName) {
        case 'overview':
            loadRecentActivity();
            break;
        case 'subscriptions':
            loadSubscriptions();
            loadPlans();
            break;
        case 'billing-history':
            loadInvoices();
            loadTransactions();
            break;
        case 'quick-pay':
            // Payment methods already loaded
            break;
    }

    PaymentsApp.tabsLoaded[tabName] = true;
}

// ========================================
// Billing History Sub-tabs
// ========================================

function switchBillingTab(tabName, button) {
    document.querySelectorAll('.billing-tab').forEach(tab => tab.classList.remove('active'));
    button.classList.add('active');

    document.getElementById('invoices-panel').style.display = tabName === 'invoices' ? 'block' : 'none';
    document.getElementById('transactions-panel').style.display = tabName === 'transactions' ? 'block' : 'none';
}

// ========================================
// API Helper
// ========================================

async function postToHandler(handler, bodyObjOrFormData) {
    const url = `/Payments?handler=${encodeURIComponent(handler)}`;
    const options = { method: 'POST', credentials: 'include' };

    if (bodyObjOrFormData instanceof FormData) {
        bodyObjOrFormData.append('__RequestVerificationToken', PaymentsApp.anti);
        options.body = bodyObjOrFormData;
    } else {
        const form = new URLSearchParams();
        for (const [k, v] of Object.entries(bodyObjOrFormData || {})) {
            form.append(k, v);
        }
        form.append('__RequestVerificationToken', PaymentsApp.anti);
        options.body = form;
    }

    const res = await fetch(url, options);
    if (!res.ok) throw new Error(await res.text());
    const ct = res.headers.get('content-type') || '';
    return ct.includes('application/json') ? res.json() : res.text();
}

async function apiCall(endpoint, options = {}) {
    const defaultOptions = {
        credentials: 'include',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': PaymentsApp.anti
        }
    };

    const response = await fetch(endpoint, { ...defaultOptions, ...options });
    return response.json();
}

// ========================================
// Overview Tab
// ========================================

async function loadRecentActivity() {
    const container = document.getElementById('recent-activity-container');
    if (!container) return;

    try {
        // Load recent invoices as activity
        const response = await fetch('/api/Invoice/GetMyInvoices?walletType=user&limit=5', {
            credentials: 'include'
        });
        const data = await response.json();

        if (data.success && data.invoices && data.invoices.length > 0) {
            container.innerHTML = data.invoices.map(invoice => `
                <div class="activity-item" style="display: flex; align-items: center; justify-content: space-between; padding: 1rem; border-bottom: 1px solid var(--gray-200);">
                    <div style="display: flex; align-items: center; gap: 1rem;">
                        <div style="width: 40px; height: 40px; background: var(--gray-100); border-radius: 8px; display: flex; align-items: center; justify-content: center;">
                            <svg width="20" height="20" fill="currentColor" viewBox="0 0 16 16" style="color: var(--gray-500);">
                                <path d="M14 4.5V14a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V2a2 2 0 0 1 2-2h5.5L14 4.5z"/>
                            </svg>
                        </div>
                        <div>
                            <div style="font-weight: 600; color: var(--gray-900);">Invoice ${invoice.invoiceNumber || invoice.stripeInvoiceId?.substring(0, 8)}</div>
                            <div style="font-size: 0.875rem; color: var(--gray-500);">${new Date(invoice.createdAt).toLocaleDateString()}</div>
                        </div>
                    </div>
                    <div style="text-align: right;">
                        <div style="font-weight: 600; color: var(--gray-900);">$${(invoice.total || 0).toFixed(2)}</div>
                        <span class="badge status-badge-${invoice.status?.toLowerCase()}">${invoice.status}</span>
                    </div>
                </div>
            `).join('');
        } else {
            container.innerHTML = `
                <div class="empty-state" style="padding: 2rem;">
                    <div class="empty-state-icon">📊</div>
                    <h3 class="empty-state-title">No recent activity</h3>
                    <p class="empty-state-description">Your recent transactions will appear here</p>
                </div>
            `;
        }
    } catch (error) {
        console.error('Error loading recent activity:', error);
        container.innerHTML = '<div class="alert alert-danger">Failed to load recent activity</div>';
    }
}

// ========================================
// Subscriptions Tab
// ========================================

async function loadSubscriptions() {
    const container = document.getElementById('subscriptions-container');
    const pastContainer = document.getElementById('past-subscriptions-container');

    try {
        const response = await fetch('/api/Subscription/GetMySubscriptions?includeInactive=false', {
            credentials: 'include'
        });
        const data = await response.json();

        if (data.success) {
            renderSubscriptions(data.subscriptions, container);

            // Load past subscriptions
            const pastResponse = await fetch('/api/Subscription/GetMySubscriptions?includeInactive=true', {
                credentials: 'include'
            });
            const pastData = await pastResponse.json();
            if (pastData.success) {
                const pastSubs = pastData.subscriptions.filter(s => s.status === 'Canceled');
                renderSubscriptions(pastSubs, pastContainer, true);
            }
        }
    } catch (error) {
        console.error('Error loading subscriptions:', error);
        container.innerHTML = '<div class="alert alert-danger">Failed to load subscriptions</div>';
    }
}

function renderSubscriptions(subscriptions, container, isPast = false) {
    if (!subscriptions || subscriptions.length === 0) {
        container.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">${isPast ? '📋' : '🎯'}</div>
                <h3 class="empty-state-title">${isPast ? 'No past subscriptions' : 'No active subscriptions'}</h3>
                <p class="empty-state-description">${isPast ? 'Your canceled subscriptions will appear here' : 'Get started by subscribing to a plan'}</p>
                ${!isPast ? '<button class="btn btn-primary mt-3" data-bs-toggle="modal" data-bs-target="#newSubscriptionModal">Subscribe Now</button>' : ''}
            </div>
        `;
        return;
    }

    const statusColors = {
        active: 'success',
        trialing: 'info',
        'past-due': 'warning',
        canceled: 'danger',
        paused: 'gray'
    };

    container.innerHTML = subscriptions.map(sub => {
        const statusClass = sub.status?.toLowerCase();
        const badgeClass = statusColors[statusClass] || 'gray';

        return `
            <div class="subscription-item">
                <div class="subscription-header">
                    <div class="subscription-info">
                        <div class="subscription-title">
                            ${sub.productName || 'Subscription'}
                            <span class="badge badge-${badgeClass}">${sub.status}</span>
                            ${sub.status === 'Trialing' ? '<span class="badge" style="background: var(--primary-gradient); color: white;">Free Trial</span>' : ''}
                        </div>
                        <div>
                            <span class="subscription-price">$${(sub.amount || 0).toFixed(2)}</span>
                            <span class="subscription-interval">/ ${sub.intervalCount > 1 ? sub.intervalCount + ' ' : ''}${sub.interval}${sub.intervalCount > 1 ? 's' : ''}</span>
                        </div>
                        <div class="subscription-meta">
                            <div class="subscription-meta-item">
                                <span class="subscription-meta-label">${sub.status === 'Trialing' ? 'Trial Ends' : 'Next Billing'}</span>
                                <span class="subscription-meta-value">${new Date(sub.status === 'Trialing' ? sub.trialEnd : sub.currentPeriodEnd).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}</span>
                            </div>
                            ${sub.cancelAtPeriodEnd ? `
                            <div class="subscription-meta-item">
                                <span class="subscription-meta-label">Status</span>
                                <span class="subscription-meta-value" style="color: var(--danger-color);">Cancels at period end</span>
                            </div>
                            ` : ''}
                        </div>
                    </div>
                    ${!isPast ? `
                    <div class="subscription-actions">
                        <button class="btn btn-sm btn-outline" onclick="manageSubscription('${sub.id}')">Manage</button>
                        ${!sub.cancelAtPeriodEnd ?
                            `<button class="btn btn-sm btn-danger" onclick="cancelSubscription('${sub.id}')">Cancel</button>` :
                            `<button class="btn btn-sm btn-success" onclick="resumeSubscription('${sub.id}')">Resume</button>`
                        }
                        <button class="btn btn-sm btn-secondary" onclick="removeStaleSubscription('${sub.id}')" title="Remove this subscription from your list">Remove</button>
                    </div>
                    ` : `
                    <div class="subscription-actions">
                        <button class="btn btn-sm btn-secondary" onclick="removeStaleSubscription('${sub.id}')" title="Remove from list">Remove</button>
                    </div>
                    `}
                </div>
            </div>
        `;
    }).join('');
}

async function loadPlans() {
    try {
        const response = await fetch('/api/Subscription/GetAvailablePlans', {
            credentials: 'include'
        });
        const data = await response.json();

        if (data.success && data.plans) {
            renderPlans(data.plans);
        }
    } catch (error) {
        console.error('Error loading plans:', error);
    }
}

function renderPlans(plans) {
    const container = document.getElementById('plans-container');
    if (!container) return;

    container.innerHTML = plans.map(plan => `
        <div class="plan-option" onclick="selectPlan('${plan.priceId}', '${plan.productName}', ${plan.amount}, '${plan.interval}', ${plan.trialPeriodDays || 0}, ${plan.enableTrial || false}, '${plan.id || ''}')">
            ${plan.isMostPopular ? `<span class="plan-badge">Most Popular</span>` : ''}
            <div class="plan-name">${plan.productName}</div>
            <div class="plan-price">$${plan.amount.toFixed(2)}</div>
            <div class="plan-interval">per ${plan.interval}</div>
            ${plan.description ? `<p class="plan-description">${plan.description}</p>` : ''}
            ${plan.enableTrial && plan.trialPeriodDays ? `
                <div class="mt-3">
                    <span class="trial-badge">
                        <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" fill="currentColor" viewBox="0 0 16 16">
                            <path d="M3 2.5a2.5 2.5 0 0 1 5 0 2.5 2.5 0 0 1 5 0v.006c0 .07 0 .27-.038.494H15a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1v7.5a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 1 14.5V7a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1h2.038A2.968 2.968 0 0 1 3 2.506V2.5z"/>
                        </svg>
                        ${plan.trialPeriodDays} days free trial
                    </span>
                </div>
            ` : ''}
        </div>
    `).join('');
}

function selectPlan(priceId, planName, amount, interval, trialDays, enableTrial, planGuid) {
    PaymentsApp.selectedPlanId = priceId;
    PaymentsApp.selectedPlanGuid = planGuid || null;
    PaymentsApp.baseTrialDays = trialDays;

    // Update UI
    document.querySelectorAll('.plan-option').forEach(card => card.classList.remove('selected'));
    event.currentTarget.classList.add('selected');

    document.getElementById('selected-plan-display').textContent = `${planName} - $${amount.toFixed(2)}/${interval}`;
    document.getElementById('subscription-details').style.display = 'block';
    document.getElementById('create-subscription-btn').style.display = 'inline-flex';

    // Handle trial section
    const trialSection = document.getElementById('trial-section');
    const trialCheckbox = document.getElementById('trial-checkbox');
    const trialDaysSpan = document.getElementById('trial-days');

    if (enableTrial && trialDays > 0) {
        trialSection.style.display = 'block';
        trialDaysSpan.textContent = trialDays;
        trialCheckbox.checked = true;
    } else {
        trialSection.style.display = 'none';
        trialCheckbox.checked = false;
    }

    // Reset promo code state
    PaymentsApp.appliedPromoCode = null;
    const existingMsg = document.querySelector('.promo-message');
    if (existingMsg) existingMsg.remove();
    const btn = document.querySelector('.btn-apply-promo');
    if (btn) {
        btn.textContent = 'Apply';
        btn.style.background = '';
        btn.style.borderColor = '';
        btn.style.color = '';
        btn.disabled = false;
    }
}

async function createSubscription() {
    const paymentMethodId = document.getElementById('payment-method-select')?.value;
    const promoCodeInput = document.getElementById('promo-code-input')?.value?.trim();
    const useTrial = document.getElementById('trial-checkbox')?.checked;
    const trialSection = document.getElementById('trial-section');
    const trialEnabled = trialSection?.style.display !== 'none';

    if (!PaymentsApp.selectedPlanId || !paymentMethodId) {
        alert('Please select a plan and payment method');
        return;
    }

    const btn = document.getElementById('create-subscription-btn');
    const btnText = btn.querySelector('span');
    btn.disabled = true;
    btnText.textContent = 'Creating...';

    try {
        const requestBody = {
            priceId: PaymentsApp.selectedPlanId,
            paymentMethodId: paymentMethodId
        };

        const promoToUse = PaymentsApp.appliedPromoCode || promoCodeInput;
        if (promoToUse) {
            requestBody.promoCode = promoToUse;
        }

        if (trialEnabled && useTrial) {
            requestBody.trialPeriodDays = parseInt(document.getElementById('trial-days').textContent);
        }

        const response = await fetch('/api/Subscription/CreateSubscription', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': PaymentsApp.anti
            },
            body: JSON.stringify(requestBody)
        });

        const data = await response.json();

        if (data.success) {
            bootstrap.Modal.getInstance(document.getElementById('newSubscriptionModal')).hide();
            alert('Subscription created successfully!');
            loadSubscriptions();
        } else {
            alert('Error: ' + (data.error || 'Unknown error occurred'));
        }
    } catch (error) {
        console.error('Error creating subscription:', error);
        alert('Failed to create subscription: ' + error.message);
    } finally {
        btn.disabled = false;
        btnText.textContent = 'Subscribe Now';
    }
}

async function cancelSubscription(subscriptionId) {
    if (!confirm('Are you sure you want to cancel this subscription? It will remain active until the end of the current billing period.')) {
        return;
    }

    try {
        const response = await fetch('/api/Subscription/CancelSubscription', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': PaymentsApp.anti
            },
            body: JSON.stringify({
                subscriptionId: subscriptionId,
                cancelImmediately: false
            })
        });

        const data = await response.json();

        if (data.success) {
            alert('Subscription canceled. It will remain active until the end of your billing period.');
            loadSubscriptions();
        } else {
            if (data.error && (data.error.includes('resource_missing') || data.error.includes('No such subscription'))) {
                if (confirm('This subscription is no longer active. Would you like to remove it from your list?')) {
                    removeStaleSubscription(subscriptionId);
                }
            } else {
                alert('Error: ' + data.error);
            }
        }
    } catch (error) {
        console.error('Error canceling subscription:', error);
        alert('Failed to cancel subscription');
    }
}

async function resumeSubscription(subscriptionId) {
    try {
        const response = await fetch('/api/Subscription/ResumeSubscription', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': PaymentsApp.anti
            },
            body: JSON.stringify({ subscriptionId: subscriptionId })
        });

        const data = await response.json();

        if (data.success) {
            alert('Subscription resumed successfully!');
            loadSubscriptions();
        } else {
            alert('Error: ' + data.error);
        }
    } catch (error) {
        console.error('Error resuming subscription:', error);
        alert('Failed to resume subscription');
    }
}

async function removeStaleSubscription(subscriptionId) {
    if (!confirm('Are you sure you want to remove this subscription from your list?')) {
        return;
    }

    try {
        const response = await fetch('/Payments?handler=RemoveStale', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': PaymentsApp.anti
            },
            body: new URLSearchParams({ subscriptionId: subscriptionId })
        });

        const data = await response.json();

        if (data.success) {
            alert('Subscription removed successfully.');
            loadSubscriptions();
        } else {
            alert('Error: ' + (data.error || 'Unknown error occurred'));
        }
    } catch (error) {
        console.error('Error removing subscription:', error);
        alert('Failed to remove subscription: ' + error.message);
    }
}

function manageSubscription(subscriptionId) {
    alert('Subscription management coming soon!');
}

async function applyPromoCode() {
    const promoInput = document.getElementById('promo-code-input');
    const promoCode = promoInput?.value?.trim();
    const btn = document.querySelector('.btn-apply-promo');

    if (!promoCode) return;

    if (!PaymentsApp.selectedPlanGuid) {
        alert('Please select a plan first');
        return;
    }

    btn.textContent = 'Checking...';
    btn.disabled = true;

    try {
        const response = await fetch('/api/Subscription/ValidatePromoCode', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': PaymentsApp.anti
            },
            body: JSON.stringify({
                code: promoCode,
                planId: PaymentsApp.selectedPlanGuid
            })
        });

        const result = await response.json();

        if (result.success) {
            PaymentsApp.appliedPromoCode = promoCode;
            btn.textContent = 'Applied!';
            btn.style.background = 'var(--success-color)';
            btn.style.borderColor = 'var(--success-color)';
            btn.style.color = 'white';
            showPromoSuccess(result.discountDisplay);
        } else {
            PaymentsApp.appliedPromoCode = null;
            btn.textContent = 'Invalid';
            btn.style.background = 'var(--danger-color)';
            btn.style.borderColor = 'var(--danger-color)';
            btn.style.color = 'white';
            showPromoError(result.error || 'Invalid promo code');

            setTimeout(() => {
                btn.textContent = 'Apply';
                btn.style.background = '';
                btn.style.borderColor = '';
                btn.style.color = '';
                btn.disabled = false;
            }, 2000);
        }
    } catch (error) {
        console.error('Error validating promo code:', error);
        btn.textContent = 'Error';
        setTimeout(() => {
            btn.textContent = 'Apply';
            btn.style.background = '';
            btn.style.borderColor = '';
            btn.style.color = '';
            btn.disabled = false;
        }, 2000);
    }
}

function showPromoSuccess(discountDisplay) {
    const existingMsg = document.querySelector('.promo-message');
    if (existingMsg) existingMsg.remove();

    const wrapper = document.querySelector('.promo-code-wrapper');
    const msgEl = document.createElement('div');
    msgEl.className = 'promo-message';
    msgEl.style.cssText = 'margin-top: 0.5rem; padding: 0.5rem 0.75rem; background: rgba(16, 185, 129, 0.1); border: 1px solid rgba(16, 185, 129, 0.3); border-radius: 6px; color: #065f46; font-size: 0.875rem; font-weight: 500;';
    msgEl.innerHTML = `<svg style="display:inline; vertical-align: -2px; margin-right: 4px;" xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="currentColor" viewBox="0 0 16 16"><path d="M16 8A8 8 0 1 1 0 8a8 8 0 0 1 16 0zm-3.97-3.03a.75.75 0 0 0-1.08.022L7.477 9.417 5.384 7.323a.75.75 0 0 0-1.06 1.06L6.97 11.03a.75.75 0 0 0 1.079-.02l3.992-4.99a.75.75 0 0 0-.01-1.05z"/></svg> Promo applied: ${discountDisplay}`;
    wrapper.parentNode.appendChild(msgEl);
}

function showPromoError(errorMessage) {
    const existingMsg = document.querySelector('.promo-message');
    if (existingMsg) existingMsg.remove();

    const wrapper = document.querySelector('.promo-code-wrapper');
    const msgEl = document.createElement('div');
    msgEl.className = 'promo-message';
    msgEl.style.cssText = 'margin-top: 0.5rem; padding: 0.5rem 0.75rem; background: rgba(220, 38, 38, 0.1); border: 1px solid rgba(220, 38, 38, 0.3); border-radius: 6px; color: #991b1b; font-size: 0.875rem; font-weight: 500;';
    msgEl.innerHTML = `<svg style="display:inline; vertical-align: -2px; margin-right: 4px;" xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="currentColor" viewBox="0 0 16 16"><path d="M16 8A8 8 0 1 1 0 8a8 8 0 0 1 16 0zM8 4a.905.905 0 0 0-.9.995l.35 3.507a.552.552 0 0 0 1.1 0l.35-3.507A.905.905 0 0 0 8 4zm.002 6a1 1 0 1 0 0 2 1 1 0 0 0 0-2z"/></svg> ${errorMessage}`;
    wrapper.parentNode.appendChild(msgEl);

    setTimeout(() => msgEl.remove(), 5000);
}

// ========================================
// Billing History Tab
// ========================================

async function loadInvoices() {
    const container = document.getElementById('invoices-container');
    if (!container) return;

    container.innerHTML = `
        <div class="text-center py-5">
            <div class="spinner spinner-primary" style="width: 40px; height: 40px; border-width: 4px;"></div>
            <p class="text-muted mt-3">Loading invoices...</p>
        </div>
    `;

    try {
        const statusFilter = document.getElementById('invoice-status-filter')?.value || '';
        const startDate = document.getElementById('invoice-start-date')?.value || '';
        const endDate = document.getElementById('invoice-end-date')?.value || '';

        let url = '/api/Invoice/GetMyInvoices?walletType=user&limit=100';
        if (statusFilter) url += `&status=${statusFilter}`;
        if (startDate) url += `&startDate=${startDate}`;
        if (endDate) url += `&endDate=${endDate}`;

        const response = await fetch(url, { credentials: 'include' });
        const data = await response.json();

        if (data.success) {
            renderInvoices(data.invoices);
        } else {
            container.innerHTML = '<div class="alert alert-danger">Failed to load invoices</div>';
        }
    } catch (error) {
        console.error('Error loading invoices:', error);
        container.innerHTML = '<div class="alert alert-danger">Error loading invoices</div>';
    }
}

function renderInvoices(invoices) {
    const container = document.getElementById('invoices-container');

    if (!invoices || invoices.length === 0) {
        container.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">📄</div>
                <h3 class="empty-state-title">No invoices found</h3>
                <p class="empty-state-description">Your invoices will appear here once you have subscriptions or make purchases.</p>
            </div>
        `;
        return;
    }

    container.innerHTML = invoices.map(invoice => {
        const statusClass = invoice.status?.toLowerCase();
        return `
            <div class="invoice-item">
                <div class="invoice-header">
                    <div class="invoice-info">
                        <div class="invoice-number">
                            Invoice ${invoice.invoiceNumber || invoice.stripeInvoiceId?.substring(0, 8)}
                            <span class="badge status-badge-${statusClass}">${invoice.status}</span>
                        </div>
                        <div>
                            <span class="invoice-amount">$${(invoice.total || 0).toFixed(2)}</span>
                            ${invoice.currency ? `<span class="invoice-currency">${invoice.currency.toUpperCase()}</span>` : ''}
                        </div>
                        ${invoice.description ? `<div style="color: var(--gray-600); margin-top: 0.5rem;">${invoice.description}</div>` : ''}
                        <div class="invoice-meta">
                            <div class="invoice-meta-item">
                                <span class="invoice-meta-label">Created</span>
                                <span class="invoice-meta-value">${new Date(invoice.createdAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}</span>
                            </div>
                            ${invoice.dueDate ? `
                            <div class="invoice-meta-item">
                                <span class="invoice-meta-label">Due Date</span>
                                <span class="invoice-meta-value">${new Date(invoice.dueDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}</span>
                            </div>
                            ` : ''}
                            ${invoice.paidAt ? `
                            <div class="invoice-meta-item">
                                <span class="invoice-meta-label">Paid</span>
                                <span class="invoice-meta-value">${new Date(invoice.paidAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}</span>
                            </div>
                            ` : ''}
                        </div>
                    </div>
                    <div class="invoice-actions">
                        <button class="btn btn-sm btn-outline" onclick="viewInvoice('${invoice.id}')">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M16 8s-3-5.5-8-5.5S0 8 0 8s3 5.5 8 5.5S16 8 16 8zM1.173 8a13.133 13.133 0 0 1 1.66-2.043C4.12 4.668 5.88 3.5 8 3.5c2.12 0 3.879 1.168 5.168 2.457A13.133 13.133 0 0 1 14.828 8c-.058.087-.122.183-.195.288-.335.48-.83 1.12-1.465 1.755C11.879 11.332 10.119 12.5 8 12.5c-2.12 0-3.879-1.168-5.168-2.457A13.134 13.134 0 0 1 1.172 8z"/>
                                <path d="M8 5.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5zM4.5 8a3.5 3.5 0 1 1 7 0 3.5 3.5 0 0 1-7 0z"/>
                            </svg>
                            View
                        </button>
                        ${invoice.invoicePdfUrl ? `
                        <a href="/api/Invoice/DownloadInvoicePdf?invoiceId=${invoice.id}" class="btn btn-sm btn-secondary" target="_blank">
                            <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                                <path d="M.5 9.9a.5.5 0 0 1 .5.5v2.5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-2.5a.5.5 0 0 1 1 0v2.5a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2v-2.5a.5.5 0 0 1 .5-.5z"/>
                                <path d="M7.646 11.854a.5.5 0 0 0 .708 0l3-3a.5.5 0 0 0-.708-.708L8.5 10.293V1.5a.5.5 0 0 0-1 0v8.793L5.354 8.146a.5.5 0 1 0-.708.708l3 3z"/>
                            </svg>
                            PDF
                        </a>
                        ` : ''}
                        ${invoice.status === 'Open' && invoice.amountRemaining > 0 ? `
                        <button class="btn btn-sm btn-success" onclick="retryPayment('${invoice.id}')">Pay Now</button>
                        ` : ''}
                    </div>
                </div>
            </div>
        `;
    }).join('');
}

async function viewInvoice(invoiceId) {
    try {
        const response = await fetch(`/api/Invoice/GetInvoice?invoiceId=${invoiceId}`, {
            credentials: 'include'
        });
        const data = await response.json();

        if (data.success) {
            renderInvoiceDetails(data.invoice);
            new bootstrap.Modal(document.getElementById('invoiceDetailsModal')).show();
        }
    } catch (error) {
        console.error('Error loading invoice details:', error);
        alert('Failed to load invoice details');
    }
}

function renderInvoiceDetails(invoice) {
    const content = document.getElementById('invoice-details-content');
    const statusClass = invoice.status?.toLowerCase();

    content.innerHTML = `
        <div class="invoice-details-section">
            <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 1rem;">
                <div>
                    <div style="font-size: 1.125rem; font-weight: 600; color: var(--gray-900); margin-bottom: 0.5rem;">
                        Invoice ${invoice.invoiceNumber || invoice.stripeInvoiceId?.substring(0, 8)}
                    </div>
                    <span class="badge status-badge-${statusClass}">${invoice.status}</span>
                </div>
                <div class="invoice-amount">$${(invoice.total || 0).toFixed(2)}</div>
            </div>
            <div style="display: grid; grid-template-columns: repeat(2, 1fr); gap: 1.5rem; margin-top: 1rem;">
                <div>
                    <div class="invoice-meta-label">Created</div>
                    <div class="invoice-meta-value">${new Date(invoice.createdAt).toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' })}</div>
                </div>
                ${invoice.dueDate ? `
                <div>
                    <div class="invoice-meta-label">Due Date</div>
                    <div class="invoice-meta-value">${new Date(invoice.dueDate).toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' })}</div>
                </div>
                ` : ''}
                ${invoice.paidAt ? `
                <div>
                    <div class="invoice-meta-label">Paid On</div>
                    <div class="invoice-meta-value">${new Date(invoice.paidAt).toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' })}</div>
                </div>
                ` : ''}
            </div>
        </div>

        ${invoice.lineItems && invoice.lineItems.length > 0 ? `
            <div class="invoice-details-section">
                <h3 style="font-size: 1.0625rem; font-weight: 600; color: var(--gray-900); margin-bottom: 1rem;">Line Items</h3>
                <div class="line-items-list">
                    ${invoice.lineItems.map(item => `
                        <div class="line-item">
                            <div class="line-item-info">
                                <div class="line-item-description">${item.description}</div>
                                <div class="line-item-quantity">Quantity: ${item.quantity}</div>
                            </div>
                            <div class="line-item-amount">$${(item.amount || 0).toFixed(2)}</div>
                        </div>
                    `).join('')}
                </div>
            </div>
        ` : ''}

        <div class="invoice-details-section">
            <div class="invoice-total-section">
                <div class="invoice-total-row">
                    <span>Subtotal:</span>
                    <span>$${(invoice.subtotal || 0).toFixed(2)}</span>
                </div>
                ${invoice.tax ? `
                    <div class="invoice-total-row">
                        <span>Tax:</span>
                        <span>$${(invoice.tax || 0).toFixed(2)}</span>
                    </div>
                ` : ''}
                <div class="invoice-total-row total">
                    <span>Total:</span>
                    <span>$${(invoice.total || 0).toFixed(2)}</span>
                </div>
            </div>
        </div>
    `;
}

async function retryPayment(invoiceId) {
    if (!confirm('Retry payment for this invoice?')) return;

    try {
        const response = await fetch('/api/Invoice/RetryPayment', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': PaymentsApp.anti
            },
            body: JSON.stringify({ invoiceId: invoiceId })
        });

        const data = await response.json();

        if (data.success) {
            alert('Payment processed successfully!');
            loadInvoices();
        } else {
            alert('Error: ' + data.error);
        }
    } catch (error) {
        console.error('Error retrying payment:', error);
        alert('Failed to process payment');
    }
}

async function loadTransactions() {
    const container = document.getElementById('transactions-container');
    if (!container) return;

    // Placeholder - implement actual transaction loading if API exists
    container.innerHTML = `
        <div class="empty-state">
            <div class="empty-state-icon">💳</div>
            <h3 class="empty-state-title">Wallet Transactions</h3>
            <p class="empty-state-description">Your wallet top-ups and charges will appear here.</p>
        </div>
    `;
}

async function syncAllData() {
    const btn = event.target;
    const originalHTML = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = `<div class="spinner" style="width: 16px; height: 16px; border-width: 2px;"></div> Syncing...`;

    try {
        const response = await fetch('/api/Invoice/SyncAllInvoices?walletType=user', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'RequestVerificationToken': PaymentsApp.anti
            }
        });

        const data = await response.json();

        if (data.success) {
            alert('Data synced successfully!');
            loadInvoices();
        } else {
            alert('Sync failed');
        }
    } catch (error) {
        console.error('Error syncing data:', error);
        alert('Failed to sync data');
    } finally {
        btn.disabled = false;
        btn.innerHTML = originalHTML;
    }
}

// ========================================
// Payment Methods
// ========================================

async function loadPaymentMethodsForSelects() {
    try {
        const response = await fetch('/api/Payment/GetPaymentMethods?paymentMethodType=User', {
            credentials: 'include'
        });

        const methods = await response.json();
        const selects = [
            document.getElementById('payment-method-select'),
            document.getElementById('quick-pay-method'),
            document.getElementById('modal-quick-pay-method')
        ];

        selects.forEach(select => {
            if (!select) return;
            if (methods && methods.length > 0) {
                select.innerHTML = methods.map(pm =>
                    `<option value="${pm.paymentMethodId}">${pm.brand || pm.bankName} •••• ${pm.last4}</option>`
                ).join('');
            } else {
                select.innerHTML = '<option value="">No payment methods - add one first</option>';
            }
        });
    } catch (error) {
        console.error('Error loading payment methods:', error);
    }
}

function initPaymentMethodHandlers() {
    // Make default
    document.querySelectorAll('.make-default').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            const pmId = e.currentTarget.dataset.pmid;
            await postToHandler('MakeDefault', { pmId, walletId: PaymentsApp.activeWalletId });
            location.reload();
        });
    });

    // Delete method
    document.querySelectorAll('.delete-method').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            const pmId = e.currentTarget.dataset.pmid;
            if (!confirm('Remove this payment method?')) return;
            await postToHandler('DeleteMethod', { pmId, walletId: PaymentsApp.activeWalletId });
            location.reload();
        });
    });
}

// ========================================
// Modals
// ========================================

function initModals() {
    // Add Funds Modal
    const addFundsForm = document.getElementById('add-funds-form');
    if (addFundsForm) {
        addFundsForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const dollars = Number(document.getElementById('topup').value || 0);
            if (!dollars || dollars < 1) {
                alert("Enter an amount >= $1");
                return;
            }
            const data = { amountCents: Math.round(dollars * 100), walletId: PaymentsApp.activeWalletId };
            const { url } = await postToHandler('AddFunds', data);
            window.location = url;
        });
    }

    // Add Payment Method Modal - Stripe Elements
    const addPmModal = document.getElementById('addPmModal');
    const paymentElementContainer = document.getElementById('payment-element');

    if (addPmModal && paymentElementContainer) {
        addPmModal.addEventListener('shown.bs.modal', async () => {
            if (PaymentsApp.isMounting || PaymentsApp.paymentElement) return;
            if (!PaymentsApp.stripe) {
                alert('Stripe is not initialized. Please refresh the page.');
                return;
            }

            try {
                PaymentsApp.isMounting = true;
                const { clientSecret } = await postToHandler('SetupIntent', { walletId: PaymentsApp.activeWalletId });
                PaymentsApp.clientSecretCurrent = clientSecret;

                paymentElementContainer.innerHTML = '';
                PaymentsApp.elements = PaymentsApp.stripe.elements({ clientSecret });
                PaymentsApp.paymentElement = PaymentsApp.elements.create('payment', { paymentMethodOrder: ['us_bank_account', 'card'] });
                PaymentsApp.paymentElement.mount('#payment-element');
            } catch (err) {
                console.error('SetupIntent error:', err);
                alert('Unable to initialize payment form: ' + (err.message || err));
            } finally {
                PaymentsApp.isMounting = false;
            }
        });

        addPmModal.addEventListener('hidden.bs.modal', () => {
            try {
                if (PaymentsApp.paymentElement) PaymentsApp.paymentElement.destroy();
            } finally {
                PaymentsApp.paymentElement = null;
                PaymentsApp.elements = null;
                PaymentsApp.clientSecretCurrent = null;
                paymentElementContainer.innerHTML = '';
                PaymentsApp.isSubmitting = false;
                PaymentsApp.isMounting = false;
            }
        });
    }

    // Payment Method Form Submit
    const pmForm = document.getElementById('pm-form');
    if (pmForm) {
        pmForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            if (PaymentsApp.isSubmitting) return;
            if (!PaymentsApp.elements || !PaymentsApp.paymentElement || !PaymentsApp.clientSecretCurrent) {
                alert('Payment form is not ready. Please reopen "Add Payment Method".');
                return;
            }

            PaymentsApp.isSubmitting = true;
            const saveBtn = document.getElementById('savePm');
            saveBtn.disabled = true;

            try {
                const { error } = await PaymentsApp.stripe.confirmSetup({
                    elements: PaymentsApp.elements,
                    confirmParams: { return_url: window.location.href }
                });
                if (error) alert(error.message);
            } catch (err) {
                alert(err?.message || 'Could not save payment method.');
            } finally {
                PaymentsApp.isSubmitting = false;
                saveBtn.disabled = false;
            }
        });
    }

    // Create Subscription Button
    const createSubBtn = document.getElementById('create-subscription-btn');
    if (createSubBtn) {
        createSubBtn.addEventListener('click', createSubscription);
    }

    // Quick Pay handlers
    const quickPaySubmit = document.getElementById('quick-pay-submit');
    if (quickPaySubmit) {
        quickPaySubmit.addEventListener('click', handleQuickPay);
    }

    const modalQuickPaySubmit = document.getElementById('modal-quick-pay-submit');
    if (modalQuickPaySubmit) {
        modalQuickPaySubmit.addEventListener('click', handleModalQuickPay);
    }
}

async function handleQuickPay() {
    const amount = parseFloat(document.getElementById('quick-pay-amount')?.value || 0);
    const description = document.getElementById('quick-pay-description')?.value || '';
    const paymentMethodId = document.getElementById('quick-pay-method')?.value;

    if (!amount || amount < 1) {
        alert('Please enter a valid amount (minimum $1)');
        return;
    }

    if (!paymentMethodId) {
        alert('Please select a payment method');
        return;
    }

    const btn = document.getElementById('quick-pay-submit');
    btn.disabled = true;
    btn.innerHTML = '<div class="spinner" style="width: 16px; height: 16px; border-width: 2px;"></div> Processing...';

    try {
        const response = await fetch('/api/Payment/QuickCharge', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': PaymentsApp.anti
            },
            body: JSON.stringify({
                amountCents: Math.round(amount * 100),
                paymentMethodId: paymentMethodId,
                description: description
            })
        });

        const data = await response.json();

        if (data.success) {
            alert('Payment successful!');
            document.getElementById('quick-pay-amount').value = '';
            document.getElementById('quick-pay-description').value = '';
        } else {
            alert('Payment failed: ' + (data.error || 'Unknown error'));
        }
    } catch (error) {
        console.error('Error processing payment:', error);
        alert('Failed to process payment');
    } finally {
        btn.disabled = false;
        btn.innerHTML = `<svg width="18" height="18" fill="currentColor" viewBox="0 0 16 16"><path d="M.5 9.9a.5.5 0 0 1 .5.5v2.5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-2.5a.5.5 0 0 1 1 0v2.5a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2v-2.5a.5.5 0 0 1 .5-.5z"/><path d="M7.646 1.146a.5.5 0 0 1 .708 0l3 3a.5.5 0 0 1-.708.708L8.5 2.707V11.5a.5.5 0 0 1-1 0V2.707L5.354 4.854a.5.5 0 1 1-.708-.708l3-3z"/></svg> Pay Now`;
    }
}

async function handleModalQuickPay() {
    const amount = parseFloat(document.getElementById('modal-quick-pay-amount')?.value || 0);
    const description = document.getElementById('modal-quick-pay-description')?.value || '';
    const paymentMethodId = document.getElementById('modal-quick-pay-method')?.value;

    if (!amount || amount < 1) {
        alert('Please enter a valid amount (minimum $1)');
        return;
    }

    if (!paymentMethodId) {
        alert('Please select a payment method');
        return;
    }

    const btn = document.getElementById('modal-quick-pay-submit');
    btn.disabled = true;
    btn.innerHTML = '<div class="spinner" style="width: 16px; height: 16px; border-width: 2px;"></div> Processing...';

    try {
        const response = await fetch('/api/Payment/QuickCharge', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': PaymentsApp.anti
            },
            body: JSON.stringify({
                amountCents: Math.round(amount * 100),
                paymentMethodId: paymentMethodId,
                description: description
            })
        });

        const data = await response.json();

        if (data.success) {
            bootstrap.Modal.getInstance(document.getElementById('quickPayModal')).hide();
            alert('Payment successful!');
        } else {
            alert('Payment failed: ' + (data.error || 'Unknown error'));
        }
    } catch (error) {
        console.error('Error processing payment:', error);
        alert('Failed to process payment');
    } finally {
        btn.disabled = false;
        btn.innerHTML = `<svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16"><path d="M.5 9.9a.5.5 0 0 1 .5.5v2.5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-2.5a.5.5 0 0 1 1 0v2.5a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2v-2.5a.5.5 0 0 1 .5-.5z"/><path d="M7.646 1.146a.5.5 0 0 1 .708 0l3 3a.5.5 0 0 1-.708.708L8.5 2.707V11.5a.5.5 0 0 1-1 0V2.707L5.354 4.854a.5.5 0 1 1-.708-.708l3-3z"/></svg> Pay Now`;
    }
}

// ========================================
// ACH Verification
// ========================================

function initAchVerification() {
    const banner = document.getElementById('ach-verify-panel');
    if (!banner) return;

    // Check if verification is pending on load
    const alreadyVisible = banner.style.display !== 'none';
    if (!alreadyVisible) {
        checkAchStatus();
    }

    // Verify buttons
    const verifyClientBtn = document.getElementById('verify-ach-client');
    const verifyServerBtn = document.getElementById('verify-ach-server');

    if (verifyClientBtn) {
        verifyClientBtn.addEventListener('click', doClientVerify);
    }
    if (verifyServerBtn) {
        verifyServerBtn.addEventListener('click', doServerVerify);
    }
}

async function checkAchStatus() {
    const params = new URLSearchParams(window.location.search);
    const siClientSecret = params.get('setup_intent_client_secret');

    try {
        const payload = {};
        if (siClientSecret) payload.clientSecret = siClientSecret;
        const res = await postToHandler('AchStatus', payload);

        if (res && res.needsVerification) {
            const banner = document.getElementById('ach-verify-panel');
            banner.style.display = 'block';

            if (res.hostedVerificationUrl) {
                const wrap = document.getElementById('ach-hosted-wrapper');
                const link = document.getElementById('ach-hosted-link');
                wrap.style.display = 'block';
                link.href = res.hostedVerificationUrl;
            }

            if (res.microdepositType) {
                const hint = document.getElementById('ach-hint');
                hint.style.display = 'block';
                hint.innerHTML = `Hint: expected verification via <b>${res.microdepositType}</b>.`;
            }

            if (res.clientSecret && !siClientSecret) {
                window.__achClientSecret = res.clientSecret;
            }
        }
    } catch (e) {
        console.warn('ACH status probe failed:', e);
    }
}

function getSiClientSecretFromUrlOrCache() {
    const params = new URLSearchParams(window.location.search);
    return params.get('setup_intent_client_secret') || window.__achClientSecret || null;
}

async function doClientVerify() {
    const siClientSecret = getSiClientSecretFromUrlOrCache();
    if (!siClientSecret) {
        alert("Missing verification token. Use 'Verify on Stripe' above or the Server Verify button.");
        return;
    }

    const a1 = document.getElementById('amt1')?.value;
    const a2 = document.getElementById('amt2')?.value;
    const code = (document.getElementById('code')?.value || '').trim();

    let result;
    if (code) {
        result = await PaymentsApp.stripe.verifyMicrodepositsForSetup({ clientSecret: siClientSecret, descriptorCode: code });
    } else if (a1 && a2) {
        result = await PaymentsApp.stripe.verifyMicrodepositsForSetup({ clientSecret: siClientSecret, amounts: [Number(a1), Number(a2)] });
    } else {
        alert('Enter both deposit amounts or the descriptor code.');
        return;
    }

    if (result.error) {
        alert(result.error.message);
        return;
    }
    location.href = window.location.pathname;
}

async function doServerVerify() {
    const payload = {
        clientSecret: getSiClientSecretFromUrlOrCache(),
        amount1: Number(document.getElementById('amt1')?.value || 0),
        amount2: Number(document.getElementById('amt2')?.value || 0),
        descriptorCode: (document.getElementById('code')?.value || '').trim()
    };

    const res = await postToHandler('VerifyAch', payload);
    if (res.status === 'succeeded') {
        location.reload();
    } else {
        alert('Verification status: ' + res.status);
    }
}

// ========================================
// Stripe Connect
// ========================================

async function setupStripeConnect() {
    const btn = document.getElementById('setup-connect-btn');
    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<div class="spinner" style="width: 18px; height: 18px; border-width: 2px;"></div> Setting up...';
    }

    try {
        const response = await fetch('/api/Payment/SetupStripeConnect', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': PaymentsApp.anti
            }
        });

        const data = await response.json();

        if (data.success && data.url) {
            window.location.href = data.url;
        } else {
            alert('Failed to set up Stripe Connect: ' + (data.error || 'Unknown error'));
            if (btn) {
                btn.disabled = false;
                btn.innerHTML = `<svg width="18" height="18" fill="currentColor" viewBox="0 0 16 16"><path d="M6.5 1A1.5 1.5 0 0 0 5 2.5V3H1.5A1.5 1.5 0 0 0 0 4.5v1.384l7.614 2.03a1.5 1.5 0 0 0 .772 0L16 5.884V4.5A1.5 1.5 0 0 0 14.5 3H11v-.5A1.5 1.5 0 0 0 9.5 1h-3zm0 1h3a.5.5 0 0 1 .5.5V3H6v-.5a.5.5 0 0 1 .5-.5z"/><path d="M0 12.5A1.5 1.5 0 0 0 1.5 14h13a1.5 1.5 0 0 0 1.5-1.5V6.85l-7.214 1.924a2.5 2.5 0 0 1-1.286 0L0 6.85v5.65z"/></svg> Set Up Stripe Connect`;
            }
        }
    } catch (error) {
        console.error('Error setting up Stripe Connect:', error);
        alert('Failed to set up Stripe Connect');
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = `<svg width="18" height="18" fill="currentColor" viewBox="0 0 16 16"><path d="M6.5 1A1.5 1.5 0 0 0 5 2.5V3H1.5A1.5 1.5 0 0 0 0 4.5v1.384l7.614 2.03a1.5 1.5 0 0 0 .772 0L16 5.884V4.5A1.5 1.5 0 0 0 14.5 3H11v-.5A1.5 1.5 0 0 0 9.5 1h-3zm0 1h3a.5.5 0 0 1 .5.5V3H6v-.5a.5.5 0 0 1 .5-.5z"/><path d="M0 12.5A1.5 1.5 0 0 0 1.5 14h13a1.5 1.5 0 0 0 1.5-1.5V6.85l-7.214 1.924a2.5 2.5 0 0 1-1.286 0L0 6.85v5.65z"/></svg> Set Up Stripe Connect`;
        }
    }
}

function openStripeDashboard() {
    window.open('https://dashboard.stripe.com/', '_blank');
}

// ========================================
// Expose Global Functions
// ========================================

window.switchTab = switchTab;
window.switchBillingTab = switchBillingTab;
window.loadInvoices = loadInvoices;
window.viewInvoice = viewInvoice;
window.retryPayment = retryPayment;
window.syncAllData = syncAllData;
window.selectPlan = selectPlan;
window.applyPromoCode = applyPromoCode;
window.cancelSubscription = cancelSubscription;
window.resumeSubscription = resumeSubscription;
window.removeStaleSubscription = removeStaleSubscription;
window.manageSubscription = manageSubscription;
window.setupStripeConnect = setupStripeConnect;
window.openStripeDashboard = openStripeDashboard;
