// Alpine data factories registered before Alpine starts.
// Loaded synchronously in App.razor BEFORE the deferred Alpine bundle.
document.addEventListener('alpine:init', () => {
    // Auto-rotating hero carousel.
    Alpine.data('heroCarousel', (count) => ({
        active: 0,
        count: count,
        paused: false,
        _timer: null,
        init() {
            if (this.count <= 1) return;
            this._timer = setInterval(() => {
                if (!this.paused) this.next();
            }, 6000);
        },
        destroy() {
            if (this._timer) clearInterval(this._timer);
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

    // Horizontal scroll helper for media rows.
    Alpine.data('rowScroller', () => ({
        scroll(dir) {
            const el = this.$refs.row;
            if (!el) return;
            el.scrollBy({ left: dir * el.clientWidth * 0.9, behavior: 'smooth' });
        }
    }));
});
