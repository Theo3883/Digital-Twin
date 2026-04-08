// signup-fields.js — JS interop for the registration form
// Manages the intl-tel-input phone widget lifecycle.

globalThis.signupFields = (function () {
    const itiInstances = {};

    function initPhoneInput(inputSelector, flagsPath) {
        const el = document.querySelector(inputSelector);
        if (!el) return;

        // Destroy any existing instance on this element first
        if (itiInstances[inputSelector]) {
            itiInstances[inputSelector].destroy();
            delete itiInstances[inputSelector];
        }

        const iti = globalThis.intlTelInput(el, {
            initialCountry: 'ro',
            // Use the locally bundled utils (already loaded via the utils script bundle)
            loadUtils: () => Promise.resolve(),
            countryOrder: ['ro', 'gb', 'fr', 'de', 'it', 'es', 'us'],
            preferredCountries: [],
            nationalMode: false,
            autoPlaceholder: 'polite',
            // Append dropdown to body so it gets proper viewport-relative fixed positioning
            // and auto-flips above/below depending on available space.
            dropdownContainer: document.body,
            // Point to local flag sprites
            flags: {
                width: 20,
                height: 15,
                flagsImagePath: flagsPath,
            },
        });

        itiInstances[inputSelector] = iti;
    }

    function getPhoneNumber(inputSelector) {
        const iti = itiInstances[inputSelector];
        if (!iti) return '';
        return iti.getNumber(); // returns E.164 e.g. "+40721234567"
    }

    function destroyPhoneInput(inputSelector) {
        const iti = itiInstances[inputSelector];
        if (iti) {
            iti.destroy();
            delete itiInstances[inputSelector];
        }
    }

    return { initPhoneInput, getPhoneNumber, destroyPhoneInput };
})();
