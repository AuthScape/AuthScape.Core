<#--
  AuthScape Keycloak registration page.
  Self-contained design mirroring OpenIddict.IDP Register.cshtml, wired to
  Keycloak's ${url.registrationAction} with standard field names.
-->
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Register - ${realm.displayName!'AuthScape'}</title>
    <link rel="stylesheet" href="${url.resourcesPath}/css/authscape.css" />
</head>
<body>
    <div class="register-container">
        <#include "brand-panel.ftl">

        <div class="form-side">
            <div class="form-wrapper">
                <div class="form-header">
                    <h1>Create your account</h1>
                    <p>Start your journey with us today</p>
                </div>

                <#if message?? && message.summary?has_content>
                    <div class="alert-${(message.type == 'error')?then('error','info')}">
                        ${kcSanitize(message.summary)?no_esc}
                    </div>
                </#if>

                <form id="kc-register-form" action="${url.registrationAction}" method="post">
                    <div class="name-row">
                        <div class="form-group">
                            <label for="firstName">First Name</label>
                            <input id="firstName" name="firstName" type="text"
                                   value="${(register.formData.firstName!'')}" placeholder="John" autocomplete="given-name" />
                            <#if messagesPerField.existsError('firstName')>
                                <span class="text-danger">${kcSanitize(messagesPerField.get('firstName'))?no_esc}</span>
                            </#if>
                        </div>
                        <div class="form-group">
                            <label for="lastName">Last Name</label>
                            <input id="lastName" name="lastName" type="text"
                                   value="${(register.formData.lastName!'')}" placeholder="Doe" autocomplete="family-name" />
                            <#if messagesPerField.existsError('lastName')>
                                <span class="text-danger">${kcSanitize(messagesPerField.get('lastName'))?no_esc}</span>
                            </#if>
                        </div>
                    </div>

                    <div class="form-group">
                        <label for="email">Email Address</label>
                        <input id="email" name="email" type="email"
                               value="${(register.formData.email!'')}" placeholder="john.doe@company.com" autocomplete="email" />
                        <#if messagesPerField.existsError('email')>
                            <span class="text-danger">${kcSanitize(messagesPerField.get('email'))?no_esc}</span>
                        </#if>
                    </div>

                    <#if !realm.registrationEmailAsUsername>
                        <div class="form-group">
                            <label for="username">Username</label>
                            <input id="username" name="username" type="text"
                                   value="${(register.formData.username!'')}" placeholder="johndoe" autocomplete="username" />
                            <#if messagesPerField.existsError('username')>
                                <span class="text-danger">${kcSanitize(messagesPerField.get('username'))?no_esc}</span>
                            </#if>
                        </div>
                    </#if>

                    <#if passwordRequired??>
                        <div class="form-group">
                            <label for="password">Password</label>
                            <input id="password" name="password" type="password"
                                   placeholder="Create a strong password" autocomplete="new-password" />
                            <#if messagesPerField.existsError('password')>
                                <span class="text-danger">${kcSanitize(messagesPerField.get('password'))?no_esc}</span>
                            </#if>
                        </div>

                        <div class="form-group">
                            <label for="password-confirm">Confirm Password</label>
                            <input id="password-confirm" name="password-confirm" type="password"
                                   placeholder="Confirm your password" autocomplete="new-password" />
                            <#if messagesPerField.existsError('password-confirm')>
                                <span class="text-danger">${kcSanitize(messagesPerField.get('password-confirm'))?no_esc}</span>
                            </#if>
                        </div>
                    </#if>

                    <#if recaptchaRequired??>
                        <div class="form-group">
                            <div class="g-recaptcha" data-size="compact" data-sitekey="${recaptchaSiteKey}"></div>
                        </div>
                    </#if>

                    <button type="submit" class="submit-btn" id="kc-register">Create Account</button>
                </form>

                <p class="signin-link">
                    Already have an account? <a href="${url.loginUrl}">Sign in</a>
                </p>
            </div>
        </div>
    </div>

    <#if recaptchaRequired??>
        <script src="https://www.google.com/recaptcha/api.js" async defer></script>
    </#if>
    <script src="${url.resourcesPath}/js/neural.js"></script>
</body>
</html>
