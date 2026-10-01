// Small browser helpers the Blazor app calls: saving files and reading Spanish aloud.
window.palabras = {
    downloadText: function (fileName, contentType, text) {
        const blob = new Blob([text], { type: contentType });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    },

    // Ask the browser not to clear this site's saved data when storage runs low.
    persistStorage: async function () {
        try {
            if (navigator.storage && navigator.storage.persist) {
                if (await navigator.storage.persisted()) return true;
                return await navigator.storage.persist();
            }
        } catch (e) { }
        return false;
    },

    canSpeak: function () {
        return 'speechSynthesis' in window && 'SpeechSynthesisUtterance' in window;
    },

    // Picks a Spanish voice if the device has one (prefers Spain / Mexico / US Spanish).
    _voice: function () {
        const voices = window.speechSynthesis.getVoices();
        const es = voices.filter(function (v) { return (v.lang || '').toLowerCase().indexOf('es') === 0; });
        const pref = ['es-es', 'es-mx', 'es-us'];
        for (let i = 0; i < pref.length; i++) {
            const v = es.find(function (x) { return x.lang.toLowerCase() === pref[i]; });
            if (v) return v;
        }
        return es[0] || null;
    },

    // Answer timer for cards. Time while the app is hidden (another app, phone locked) doesn't count.
    _timer: { start: 0, hiddenMs: 0, hiddenSince: null },

    timerStart: function () {
        const t = window.palabras._timer;
        t.start = performance.now();
        t.hiddenMs = 0;
        t.hiddenSince = document.hidden ? performance.now() : null;
    },

    timerElapsed: function () {
        const t = window.palabras._timer;
        if (!t.start) return 0;
        const now = performance.now();
        const hidden = t.hiddenMs + (t.hiddenSince !== null ? now - t.hiddenSince : 0);
        return Math.max(0, now - t.start - hidden);
    },

    focusById: function (id) {
        const el = document.getElementById(id);
        if (el) el.focus({ preventScroll: true });
    },

    speak: function (text, slow) {
        if (!window.palabras.canSpeak() || !text) return false;
        const synth = window.speechSynthesis;
        synth.cancel();
        const u = new SpeechSynthesisUtterance(text);
        const v = window.palabras._voice();
        if (v) u.voice = v;
        u.lang = v ? v.lang : 'es-ES';
        u.rate = slow ? 0.7 : 0.95;
        synth.speak(u);
        return true;
    }
};

// Some browsers load voices asynchronously; touching the list early warms it up.
if ('speechSynthesis' in window) {
    window.speechSynthesis.getVoices();
    window.speechSynthesis.onvoiceschanged = function () { window.speechSynthesis.getVoices(); };
}

// Pause the answer timer while the app is in the background.
document.addEventListener('visibilitychange', function () {
    const t = window.palabras._timer;
    if (document.hidden) {
        if (t.hiddenSince === null) t.hiddenSince = performance.now();
    } else if (t.hiddenSince !== null) {
        t.hiddenMs += performance.now() - t.hiddenSince;
        t.hiddenSince = null;
    }
});
