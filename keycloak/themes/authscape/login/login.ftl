<#--
  AuthScape Keycloak login page.
  Self-contained (does not extend the base template) so it can reproduce the
  OpenIddict.IDP split-screen design exactly, while wiring the form to Keycloak's
  ${url.loginAction} endpoint and standard field names (username/password/rememberMe).
-->
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Log in - ${realm.displayName!'AuthScape'}</title>
    <link rel="stylesheet" href="${url.resourcesPath}/css/authscape.css" />
</head>
<body>
    <div class="login-container">
        <#include "brand-panel.ftl">

        <div class="form-side">
            <div class="form-wrapper">
                <div class="form-header">
                    <h1>Welcome back</h1>
                    <p>Sign in to continue to your account</p>
                </div>

                <#-- Global / per-field error banner -->
                <#if message?? && message.summary?has_content>
                    <div class="alert-${(message.type == 'error')?then('error','info')}">
                        ${kcSanitize(message.summary)?no_esc}
                    </div>
                </#if>

                <#-- Social / identity providers -->
                <#if social?? && social.providers?? && social.providers?has_content>
                    <div class="social-login">
                        <#list social.providers as p>
                            <a class="social-btn" href="${p.loginUrl}">
                                <#if p.alias?lower_case == 'google'>
                                    <svg width="18" height="18" viewBox="0 0 24 24"><path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/><path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/><path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"/><path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/></svg>
                                </#if>
                                ${p.displayName!}
                            </a>
                        </#list>
                    </div>
                    <div class="divider"><span>or continue with email</span></div>
                </#if>

                <#if realm.password>
                <form id="kc-form-login" action="${url.loginAction}" method="post">
                    <div class="form-group">
                        <label for="username">
                            <#if !realm.loginWithEmailAllowed>Username<#elseif !realm.registrationEmailAsUsername>Username or email<#else>Email Address</#if>
                        </label>
                        <input id="username" name="username" type="text"
                               value="${(login.username!'')}"
                               placeholder="name@company.com"
                               autofocus autocomplete="username"
                               <#if usernameHidden??>disabled</#if> />
                        <#if messagesPerField.existsError('username','password')>
                            <span class="text-danger">${kcSanitize(messagesPerField.getFirstError('username','password'))?no_esc}</span>
                        </#if>
                    </div>

                    <div class="form-group">
                        <div class="form-row">
                            <label for="password">Password</label>
                            <#if realm.resetPasswordAllowed>
                                <a class="forgot-link" href="${url.loginResetCredentialsUrl}">Forgot password?</a>
                            </#if>
                        </div>
                        <input id="password" name="password" type="password"
                               placeholder="Enter your password" autocomplete="current-password" />
                    </div>

                    <#if realm.rememberMe && !usernameHidden??>
                        <div class="remember-me">
                            <input id="rememberMe" name="rememberMe" type="checkbox" <#if login.rememberMe??>checked</#if> />
                            <label for="rememberMe">Remember me</label>
                        </div>
                    </#if>

                    <input type="hidden" id="id-hidden-input" name="credentialId"
                           <#if auth.selectedCredential?has_content>value="${auth.selectedCredential}"</#if> />
                    <button class="submit-btn" name="login" id="kc-login" type="submit">Sign In</button>
                </form>
                </#if>

                <#if realm.registrationAllowed && !registrationDisabled??>
                    <p class="signup-link">
                        Don't have an account? <a href="${url.registrationUrl}">Sign up</a>
                    </p>
                </#if>
            </div>
        </div>
    </div>

    <script src="${url.resourcesPath}/js/neural.js"></script>
</body>
</html>
