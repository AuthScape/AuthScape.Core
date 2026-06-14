// Neural network animation for the AuthScape brand panel (matches the IDP).
(function () {
    var canvas = document.getElementById('neural-canvas');
    if (!canvas) return;
    var ctx = canvas.getContext('2d');
    var nodes = [];

    function initNodes() {
        nodes = [];
        var nodeCount = Math.floor((canvas.width * canvas.height) / 15000);
        for (var i = 0; i < nodeCount; i++) {
            nodes.push({
                x: Math.random() * canvas.width,
                y: Math.random() * canvas.height,
                vx: (Math.random() - 0.5) * 0.5,
                vy: (Math.random() - 0.5) * 0.5,
                radius: Math.random() * 2 + 1,
                opacity: Math.random() * 0.5 + 0.2
            });
        }
    }

    function resize() {
        canvas.width = canvas.parentElement.offsetWidth;
        canvas.height = canvas.parentElement.offsetHeight;
        initNodes();
    }

    function drawNetwork() {
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        for (var i = 0; i < nodes.length; i++) {
            for (var j = i + 1; j < nodes.length; j++) {
                var dx = nodes[i].x - nodes[j].x;
                var dy = nodes[i].y - nodes[j].y;
                var dist = Math.sqrt(dx * dx + dy * dy);
                if (dist < 150) {
                    var opacity = (1 - dist / 150) * 0.3;
                    ctx.beginPath();
                    ctx.moveTo(nodes[i].x, nodes[i].y);
                    ctx.lineTo(nodes[j].x, nodes[j].y);
                    ctx.strokeStyle = 'rgba(99, 179, 237, ' + opacity + ')';
                    ctx.lineWidth = 0.5;
                    ctx.stroke();
                }
            }
        }
        for (var k = 0; k < nodes.length; k++) {
            var node = nodes[k];
            ctx.beginPath();
            ctx.arc(node.x, node.y, node.radius, 0, Math.PI * 2);
            ctx.fillStyle = 'rgba(99, 179, 237, ' + node.opacity + ')';
            ctx.fill();
            ctx.beginPath();
            ctx.arc(node.x, node.y, node.radius * 2, 0, Math.PI * 2);
            ctx.fillStyle = 'rgba(99, 179, 237, ' + (node.opacity * 0.3) + ')';
            ctx.fill();
            node.x += node.vx;
            node.y += node.vy;
            if (node.x < 0 || node.x > canvas.width) node.vx *= -1;
            if (node.y < 0 || node.y > canvas.height) node.vy *= -1;
        }
        requestAnimationFrame(drawNetwork);
    }

    window.addEventListener('resize', resize);
    resize();
    drawNetwork();
})();
