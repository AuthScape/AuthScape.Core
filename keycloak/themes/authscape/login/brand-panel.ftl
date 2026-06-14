<#-- Animated AuthScape brand panel (left side). Shared by login.ftl and register.ftl. -->
<div class="brand-side">
    <div class="grid-lines"></div>
    <div class="glow-orb glow-orb-1"></div>
    <div class="glow-orb glow-orb-2"></div>
    <div class="glow-orb glow-orb-3"></div>

    <div class="particles">
        <#list 1..20 as i><div class="particle"></div></#list>
    </div>

    <canvas id="neural-canvas"></canvas>

    <div class="brand-content">
        <div class="brand-logo">${realm.displayNameHtml!realm.displayName!'AuthScape'}</div>
        <p class="brand-tagline">Secure, intelligent identity management for the modern enterprise.</p>

        <div class="features-list">
            <div class="feature-item">
                <div class="feature-icon">&#128274;</div>
                <div class="feature-text">
                    <h4>Enterprise Security</h4>
                    <p>Bank-grade encryption &amp; compliance</p>
                </div>
            </div>
            <div class="feature-item">
                <div class="feature-icon">&#9889;</div>
                <div class="feature-text">
                    <h4>Lightning Fast</h4>
                    <p>Sub-second authentication</p>
                </div>
            </div>
            <div class="feature-item">
                <div class="feature-icon">&#127760;</div>
                <div class="feature-text">
                    <h4>Global Scale</h4>
                    <p>Multi-region deployment ready</p>
                </div>
            </div>
        </div>
    </div>
</div>
