// Alpine data factories registered before Alpine starts.
// Loaded synchronously in App.razor BEFORE the deferred Alpine bundle.
document.addEventListener('alpine:init', () => {
    // Auto-rotating hero carousel.
    // Visibility is driven purely by `active`; the .razor binds opacity from it.
    Alpine.data('heroCarousel', (count) => ({
        active: 0,
        count: count,
        paused: false,
        _timer: null,
        init() {
            this._start();
        },
        destroy() {
            this._stop();
        },
        _start() {
            this._stop();
            if (this.count <= 1) return;
            this._timer = setInterval(() => {
                if (!this.paused) this.next();
            }, 6000);
        },
        _stop() {
            if (this._timer) {
                clearInterval(this._timer);
                this._timer = null;
            }
        },
        next() {
            this.active = (this.active + 1) % this.count;
        },
        prev() {
            this.active = (this.active - 1 + this.count) % this.count;
        },
        go(i) {
            if (i < 0 || i >= this.count) return;
            this.active = i;
        }
    }));

    // Recent searches — persists the last few queries in localStorage.
    // Lives on the page (outside the reactive component) so it survives the
    // Idiomorph DOM swaps that happen on every search dispatch.
    Alpine.data('recentSearches', () => ({
        items: [],
        _key: 'mm.recentSearches',
        _max: 6,
        _settleTimer: null,
        init() {
            this.items = this._load();
            // After each reactive morph, record the *settled* query — wait for
            // typing to stop so prefixes ("bat", "batm") don't get stored.
            this.$root.addEventListener('reactive:updated', () => this._scheduleRecord());
        },
        _scheduleRecord() {
            clearTimeout(this._settleTimer);
            this._settleTimer = setTimeout(() => this._record(), 1200);
        },
        _record() {
            const input = this.$root.querySelector('[data-bind="Query"]');
            const q = input?.value.trim();
            if (!q) return;
            // Move-to-front, dedupe (case-insensitive), cap at _max.
            const lower = q.toLowerCase();
            this.items = [q, ...this.items.filter((x) => x.toLowerCase() !== lower)].slice(0, this._max);
            this._save();
        },
        apply(q) {
            const input = this.$root.querySelector('[data-bind="Query"]');
            if (!input) return;
            input.value = q;
            // Trigger ReactiveBlazor's delegated input handler so it searches.
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.focus();
        },
        clearAll() {
            this.items = [];
            this._save();
        },
        _load() {
            try {
                const raw = localStorage.getItem(this._key);
                const parsed = raw ? JSON.parse(raw) : [];
                return Array.isArray(parsed) ? parsed.slice(0, this._max) : [];
            } catch {
                return [];
            }
        },
        _save() {
            try {
                localStorage.setItem(this._key, JSON.stringify(this.items));
            } catch {
                /* storage unavailable (private mode / quota) — non-fatal */
            }
        }
    }));

    // Ad-blocker advisory note on the watch page. Hidden once dismissed; the choice persists in
    // localStorage so it doesn't reappear on reload or after Idiomorph subtree swaps.
    Alpine.data('playbackTip', () => ({
        show: false,
        _key: 'mm.playbackTipDismissed',
        init() {
            try {
                this.show = localStorage.getItem(this._key) !== '1';
            } catch {
                this.show = true; // storage unavailable (private mode) — just show it
            }
        },
        dismiss() {
            this.show = false;
            try {
                localStorage.setItem(this._key, '1');
            } catch {
                /* non-fatal */
            }
        }
    }));

    // Horizontal scroll helper for media rows.
    Alpine.data('rowScroller', () => ({
        scroll(dir) {
            const el = this.$refs.row;
            if (!el) return;
            el.scrollBy({ left: dir * el.clientWidth * 0.9, behavior: 'smooth' });
        }
    }));
});

// ---------------------------------------------------------------------------
// Top navigation progress bar.
// Bracketed by Blazor's own enhanced-navigation lifecycle events:
//   enhancednavigationstart -> begin           (fires once when nav starts)
//   enhancednavigationend   -> complete         (fires once when nav settles)
// We deliberately do NOT use `enhancedload` for completion: it fires multiple
// times per navigation (once per streaming-render update), which previously
// caused the bar to finish, reset, and restart mid-load.
// ---------------------------------------------------------------------------
const navProgress = (() => {
    let trickle = null;
    let done = null;
    let progress = 0;
    let active = false;

    const bar = () => document.getElementById('nav-progress');

    function set(value) {
        const el = bar();
        if (!el) return;
        progress = Math.min(value, 1);
        el.style.opacity = '1';
        el.style.transition = 'transform 200ms ease-out, opacity 200ms ease-out';
        el.style.transform = `scaleX(${progress})`;
    }

    function start() {
        const el = bar();
        if (!el) return;
        if (active) return;
        active = true;
        progress = 0;

        // Snap to 0 width with no transition, then ease forward.
        el.style.transition = 'none';
        el.style.transform = 'scaleX(0)';
        el.style.opacity = '1';
        // Force reflow so the snap-to-0 takes effect before we animate.
        void el.offsetWidth;

        set(0.1);
        clearInterval(trickle);
        clearTimeout(done);
        trickle = setInterval(() => {
            // Creep toward — but never reach — completion.
            if (progress < 0.9) set(progress + (0.9 - progress) * 0.15);
        }, 300);
    }

    function complete() {
        const el = bar();
        if (!el) return;
        if (!active) return;
        active = false;
        clearInterval(trickle);
        set(1);
        done = setTimeout(() => {
            el.style.transition = 'opacity 300ms ease-out';
            el.style.opacity = '0';
            // Reset width after it has faded out, ready for the next run.
            setTimeout(() => {
                if (!active) {
                    el.style.transition = 'none';
                    el.style.transform = 'scaleX(0)';
                }
            }, 300);
        }, 200);
    }

    return { start, complete };
})();

// This file is loaded synchronously in <head> (so the alpine:init factories
// above are registered before Alpine boots), but blazor.web.js loads at the
// end of <body>. So the `Blazor` global doesn't exist when this script runs —
// we must wait for it before wiring up any Blazor.addEventListener handlers.
function whenBlazorReady(callback) {
    if (typeof Blazor !== 'undefined') {
        callback();
        return;
    }
    const poll = setInterval(() => {
        if (typeof Blazor !== 'undefined') {
            clearInterval(poll);
            callback();
        }
    }, 50);
}

whenBlazorReady(() => {
    // Drive the bar from Blazor's enhanced-navigation lifecycle.
    Blazor.addEventListener('enhancednavigationstart', () => {
        navProgress.start();
        // The nav header lives in the persistent layout, so its Alpine `open`
        // state survives navigation. Force the mobile dropdown closed whenever a
        // navigation begins — otherwise it stays open after tapping a link, and a
        // morph could even leave it open on the next page.
        const header = document.querySelector('[data-nav-header]');
        if (header?._x_dataStack) {
            Alpine.evaluate(header, 'open = false');
        }
    });
    Blazor.addEventListener('enhancednavigationend', () => navProgress.complete());

    // Blazor Enhanced Navigation morphs the DOM in place. Alpine only scans for
    // `x-data` on its initial boot, so components inserted by a page swap arrive
    // "dead". After each enhanced load we initialize ONLY the subtrees Alpine
    // hasn't seen yet.
    //
    // We must NOT destroy/re-init already-initialized components here. Two reasons:
    //   1. enhancedload fires multiple times per navigation (once per streaming
    //      update), so re-initializing would thrash a component repeatedly.
    //   2. Persistent components in the layout (the nav menu, modals) survive
    //      navigation with live state; tearing them down detaches their event
    //      listeners and leaves buttons dead. Leave healthy components alone —
    //      Alpine's own MutationObserver disposes ones that get removed.
    Blazor.addEventListener('enhancedload', () => {
        if (typeof Alpine === 'undefined') return;
        document.querySelectorAll('[x-data]').forEach((el) => {
            // _x_dataStack is set by Alpine once a node has been initialized.
            if (!el._x_dataStack) Alpine.initTree(el);
        });
    });

    // After enhanced navigation, if the URL ends in #<id>, scroll that element
    // into view. Enhanced nav skips the browser's native fragment handling, so
    // we re-add it here. Used by the trailer page side-panel: each video link
    // appends `#player` so picking a clip on mobile pops the player back into
    // view instead of leaving the user staring at the panel.
    Blazor.addEventListener('enhancednavigationend', () => {
        const hash = window.location.hash;
        if (!hash || hash.length < 2) return;
        const target = document.getElementById(hash.slice(1));
        if (!target) return;
        // requestAnimationFrame so the layout has settled before measuring.
        requestAnimationFrame(() => {
            target.scrollIntoView({ behavior: 'smooth', block: 'start' });
        });
    });
});

// Session-expiry recovery for reactive dispatches.
//
// The library auto-reloads on a 401 (re-runs the auth pipeline → login redirect). But when the
// auth cookie is gone, the per-user antiforgery token baked into the page no longer matches, so
// the dispatch fails antiforgery with a 400 *before* [Authorize] is evaluated — which the library
// only surfaces as a `reactive:error`. We treat that 400 (and 403) the same way: reload the
// current URL so the server either redirects to login (if unauthenticated) or re-issues a fresh
// antiforgery token (if the session is somehow still valid).
//
// Guarded by a short sessionStorage cooldown so a genuinely persistent 400 can't cause a reload
// loop — at most one auto-reload per 10s window.
document.addEventListener('reactive:error', (e) => {
    const status = e.detail && e.detail.status;
    if (status !== 400 && status !== 403) return;

    const key = 'mm.reactiveAuthReloadAt';
    const now = Date.now();
    let last = 0;
    try { last = parseInt(sessionStorage.getItem(key) || '0', 10); } catch { /* ignore */ }

    if (now - last < 10000) {
        // Already reloaded recently and still failing — don't loop. Leave the error visible.
        console.warn('Reactive dispatch still failing after a recent reload; not reloading again.');
        return;
    }

    try { sessionStorage.setItem(key, String(now)); } catch { /* ignore */ }
    window.location.assign(window.location.href);
});
