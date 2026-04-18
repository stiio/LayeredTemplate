// ---------- Password toggle ----------
document.addEventListener('click', function (e) {
    var btn = e.target.closest('.password-toggle');
    if (!btn) return;
    var input = btn.parentElement.querySelector('input');
    if (!input) return;
    var isPassword = input.type === 'password';
    input.type = isPassword ? 'text' : 'password';
    btn.textContent = isPassword ? '\u25C9' : '\u25C8';
});

// ---------- Copy-to-clipboard ----------
document.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-copy]');
    if (!btn) return;
    e.preventDefault();
    var text = btn.getAttribute('data-copy');
    if (!text || !navigator.clipboard) return;
    navigator.clipboard.writeText(text).then(function () {
        btn.classList.add('copied');
        setTimeout(function () { btn.classList.remove('copied'); }, 1500);
    }).catch(function () { /* noop */ });
});

// ---------- Resend cooldown timer ----------
(function () {
    var btn = document.querySelector('[data-resend-cooldown]');
    if (!btn) return;
    var seconds = parseInt(btn.getAttribute('data-resend-cooldown'), 10) || 60;
    var originalText = btn.textContent;
    btn.disabled = true;
    btn.style.opacity = '0.5';
    btn.style.cursor = 'not-allowed';
    var timer = setInterval(function () {
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

// ---------- Phone formatting: lazy-loaded only when the page has phone elements ----------
// libphonenumber-js/mobile is ~85KB gzipped — not worth pulling on Login/Register/Admin-apps
// etc. where there are no phone inputs. We scan the DOM once for markers and only then import;
// the browser caches the module after first visit, so subsequent pages pay nothing.
if (document.querySelector('input[data-phone-input], [data-phone-display]')) {
    import('https://esm.sh/libphonenumber-js@1.11.14/mobile')
        .then(setupPhoneFormatting)
        .catch(function (err) { console.error('Failed to load phone library', err); });
}

function setupPhoneFormatting(lib) {
    var AsYouType = lib.AsYouType;
    var parsePhoneNumberFromString = lib.parsePhoneNumberFromString;

    function digitsBefore(str, position) {
        var count = 0;
        for (var i = 0; i < position && i < str.length; i++) {
            var c = str.charCodeAt(i);
            if (c >= 48 && c <= 57) count++;
        }
        return count;
    }

    function positionAfterDigitN(str, n) {
        if (n <= 0) return 0;
        var seen = 0;
        for (var i = 0; i < str.length; i++) {
            var c = str.charCodeAt(i);
            if (c >= 48 && c <= 57) {
                seen++;
                if (seen === n) return i + 1;
            }
        }
        return str.length;
    }

    function formatPhoneInput(input) {
        var defaultCountry = input.dataset.phoneDefaultCountry || undefined;

        // Only track caret when the input is focused — on the pre-initial pass we have no
        // meaningful selection, and touching setSelectionRange off-focus can force scroll/focus
        // in some browsers.
        var isFocused = document.activeElement === input;
        var caretDigits = 0;
        if (isFocused) {
            var caret = input.selectionStart == null ? input.value.length : input.selectionStart;
            caretDigits = digitsBefore(input.value, caret);
        }

        var value = input.value;
        // International mode: strip anything non-digit, re-add the leading '+'.
        if (!defaultCountry) {
            var onlyDigits = value.replace(/[^\d]/g, '');
            value = onlyDigits ? '+' + onlyDigits : '';
        }

        var formatted = new AsYouType(defaultCountry).input(value);
        if (formatted === input.value) return;

        input.value = formatted;
        if (isFocused) {
            var newCaret = positionAfterDigitN(formatted, caretDigits);
            input.setSelectionRange(newCaret, newCaret);
        }
    }

    // Live formatting on each keystroke. Cursor tracked by digit-offset so it stays in place.
    document.addEventListener('input', function (e) {
        var input = e.target;
        if (!(input instanceof HTMLInputElement) || !input.hasAttribute('data-phone-input')) return;
        formatPhoneInput(input);
    });

    // Pre-initial formatting — run once on load for inputs that were pre-filled server-side
    // (e.g. Admin/Users/Edit loads the existing phone as E.164; we want it pretty on first paint).
    document.querySelectorAll('input[data-phone-input]').forEach(function (input) {
        if (input.value) formatPhoneInput(input);
    });

    // Before the form is submitted, replace the visible (formatted) value with canonical E.164.
    document.addEventListener('submit', function (e) {
        if (!(e.target instanceof HTMLFormElement)) return;
        e.target.querySelectorAll('input[data-phone-input]').forEach(function (input) {
            var raw = input.value.trim();
            if (!raw) return;
            var parsed = parsePhoneNumberFromString(raw, input.dataset.phoneDefaultCountry || undefined);
            if (parsed && parsed.isValid()) {
                input.value = parsed.number;
            }
        });
    }, true);

    // Format read-only displays into international style (e.g. "+14155551234" → "+1 415 555 1234").
    document.querySelectorAll('[data-phone-display]').forEach(function (el) {
        var raw = el instanceof HTMLInputElement ? el.value : el.textContent;
        if (!raw) return;
        var parsed = parsePhoneNumberFromString(raw.trim());
        if (!parsed) return;
        var pretty = parsed.formatInternational();
        if (el instanceof HTMLInputElement) {
            el.value = pretty;
        } else {
            el.textContent = pretty;
        }
    });
}
