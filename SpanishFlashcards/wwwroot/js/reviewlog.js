// The review history: every answer on a word card, kept in IndexedDB (it grows too big for localStorage).
// Each call fails quietly (returns false / []) when IndexedDB isn't available, e.g. some private windows.
window.palabrasLog = (function () {
    const DB = 'palabras-history';
    const STORE = 'answers';
    let opening = null;

    function open() {
        if (opening) return opening;
        opening = new Promise(function (resolve, reject) {
            const req = indexedDB.open(DB, 1);
            req.onupgradeneeded = function () {
                req.result.createObjectStore(STORE, { autoIncrement: true });
            };
            req.onsuccess = function () { resolve(req.result); };
            req.onerror = function () { opening = null; reject(req.error); };
        });
        return opening;
    }

    // Runs fn(store) in one transaction; resolves with the value of the request fn returns (if any).
    function run(mode, fn) {
        return open().then(function (db) {
            return new Promise(function (resolve, reject) {
                const tx = db.transaction(STORE, mode);
                const req = fn(tx.objectStore(STORE));
                tx.oncomplete = function () { resolve(req ? req.result : true); };
                tx.onerror = function () { reject(tx.error); };
                tx.onabort = function () { reject(tx.error); };
            });
        });
    }

    return {
        add: function (entry) {
            return run('readwrite', function (s) { s.add(entry); }).then(function () { return true; }, function () { return false; });
        },
        all: function () {
            return run('readonly', function (s) { return s.getAll(); }).then(function (r) { return r || []; }, function () { return []; });
        },
        count: function () {
            return run('readonly', function (s) { return s.count(); }).then(function (n) { return n || 0; }, function () { return 0; });
        },
        // Replaces the whole history (Import).
        replace: function (entries) {
            return run('readwrite', function (s) {
                s.clear();
                for (let i = 0; i < entries.length; i++) s.add(entries[i]);
            }).then(function () { return true; }, function () { return false; });
        },
        clear: function () {
            return run('readwrite', function (s) { s.clear(); }).then(function () { return true; }, function () { return false; });
        }
    };
})();
