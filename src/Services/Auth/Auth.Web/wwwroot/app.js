// Password toggle
document.addEventListener('click', function(e) {
    var btn = e.target.closest('.password-toggle');
    if (!btn) return;
    var input = btn.parentElement.querySelector('input');
    if (!input) return;
    var isPassword = input.type === 'password';
    input.type = isPassword ? 'text' : 'password';
    btn.textContent = isPassword ? '\u25C9' : '\u25C8';
});

// Auto-prefix "+" on phone inputs
document.addEventListener('input', function(e) {
    if (!e.target.hasAttribute('data-phone-prefix')) return;
    var v = e.target.value.replace(/[^\d+]/g, '');
    if (v && v[0] !== '+') v = '+' + v;
    e.target.value = v;
});

// Resend cooldown timer: add data-resend-cooldown="60" to a button
(function() {
    var btn = document.querySelector('[data-resend-cooldown]');
    if (!btn) return;
    var seconds = parseInt(btn.getAttribute('data-resend-cooldown'), 10) || 60;
    var originalText = btn.textContent;
    btn.disabled = true;
    btn.style.opacity = '0.5';
    btn.style.cursor = 'not-allowed';
    var timer = setInterval(function() {
        seconds--;
        btn.textContent = originalText + ' (' + seconds + 's)';
        if (seconds <= 0) {
            clearInterval(timer);
            btn.textContent = originalText;
            btn.disabled = false;
            btn.style.opacity = '1';
            btn.style.cursor = 'pointer';
        }
    }, 1000);
})();
