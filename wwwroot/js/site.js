// Mobile menu + dropdowns
(function () {
	var header = document.getElementById('siteHeader');
	var toggle = document.getElementById('navToggle');
	if (!header || !toggle) return;

	window.matchMedia('(min-width: 1101px)').addEventListener('change', function (e) { if (e.matches) { header.classList.remove('nav-open'); document.body.classList.remove('no-scroll'); toggle.setAttribute('aria-expanded', 'false'); } });
	toggle.addEventListener('click', function () {
		var open = header.classList.toggle('nav-open');
		toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
		document.body.classList.toggle('no-scroll', open);
	});

	// Sub menus: click toggles (mobile). On desktop they open on hover via CSS.
	var mobile = window.matchMedia('(max-width: 1100px)');
	header.querySelectorAll('.has-sub > .sub-toggle').forEach(function (btn) {
		btn.addEventListener('click', function () {
			if (!mobile.matches) return;
			var li = btn.parentElement;
			var open = li.classList.toggle('open');
			btn.setAttribute('aria-expanded', open ? 'true' : 'false');
		});
	});
})();

// Hero carousel: consecutive Hero blocks become slides
(function () {
	document.querySelectorAll('[data-carousel]').forEach(function (root) {
		var slides = root.querySelectorAll('.hero');
		var i = 0;
		var prev = root.querySelector('.carousel-prev');
		var next = root.querySelector('.carousel-next');
		function show(n) {
			i = (n + slides.length) % slides.length;
			slides.forEach(function (s, k) { s.classList.toggle('is-active', k === i); });
			if (prev) prev.classList.toggle('dim', slides.length < 2);
		}
		if (prev) prev.addEventListener('click', function () { show(i - 1); });
		if (next) next.addEventListener('click', function () { show(i + 1); });
		var timer = null;
		function auto() { if (slides.length > 1) timer = setInterval(function () { show(i + 1); }, 7000); }
		root.addEventListener('mouseenter', function () { clearInterval(timer); });
		root.addEventListener('mouseleave', auto);
		root.classList.add('is-ready'); show(0); auto();
	});
})();

// Card carousels (Events / News): arrows scroll one card
(function () {
	document.querySelectorAll('[data-cards]').forEach(function (root) {
		var track = root.querySelector('.card-track');
		var prev = root.querySelector('.card-prev');
		var next = root.querySelector('.card-next');
		function step() { var c = track.querySelector('.card'); return c ? c.getBoundingClientRect().width + 32 : 400; }
		function update() {
			prev.classList.toggle('dim', track.scrollLeft < 8);
			next.classList.toggle('dim', track.scrollLeft + track.clientWidth >= track.scrollWidth - 8);
		}
		prev.addEventListener('click', function () { track.scrollBy({ left: -step() }); });
		next.addEventListener('click', function () { track.scrollBy({ left: step() }); });
		track.addEventListener('scroll', update, { passive: true });
		window.addEventListener('resize', update);
		update();
	});
})();

// Testimonials slider
(function () {
	document.querySelectorAll('[data-testimonials]').forEach(function (root) {
		var q = root.querySelectorAll('.quote'), i = 0;
		function show(n) { i = (n + q.length) % q.length; q.forEach(function (e, k) { e.classList.toggle('active', k === i); }); }
		var p = root.querySelector('.t-prev'), n = root.querySelector('.t-next');
		if (p) p.addEventListener('click', function () { show(i - 1); });
		if (n) n.addEventListener('click', function () { show(i + 1); });
		show(0);
	});
})();

// FAQ accordion
(function () {
	document.querySelectorAll('.faq-q').forEach(function (b) {
		b.addEventListener('click', function () {
			var item = b.parentElement, open = item.classList.toggle('open');
			b.setAttribute('aria-expanded', open ? 'true' : 'false');
		});
	});
})();

// Partner directory filter
(function () {
	document.querySelectorAll('[data-directory]').forEach(function (root) {
		var chips = root.querySelectorAll('.chip'), items = root.querySelectorAll('.pd-item');
		chips.forEach(function (c) {
			c.addEventListener('click', function () {
				chips.forEach(function (x) { x.classList.toggle('active', x === c); });
				var cat = c.getAttribute('data-cat');
				items.forEach(function (it) {
					var show = !cat || it.getAttribute('data-cat') === cat;
					if (show) { it.hidden = false; void it.offsetWidth; it.classList.remove('out'); }
					else { it.classList.add('out'); setTimeout(function () { if (it.classList.contains('out')) it.hidden = true; }, 260); }
				});
			});
		});
	});
})();

// Popups: open from links to #popup-<id> (or automatically once per visit); close with X, backdrop or Esc
(function () {
	var popups = document.querySelectorAll('[data-popup]');
	if (!popups.length) return;
	var lastFocus = null;
	function open(p) { lastFocus = document.activeElement; p.hidden = false; void p.offsetWidth; p.classList.add('open'); document.body.classList.add('no-scroll'); var c = p.querySelector('.popup-close'); if (c) c.focus(); }
	function close(p) { p.classList.remove('open'); document.body.classList.remove('no-scroll'); setTimeout(function () { if (!p.classList.contains('open')) p.hidden = true; }, 300); if (lastFocus && lastFocus.focus) lastFocus.focus(); }
	popups.forEach(function (p) {
		p.querySelectorAll('[data-popup-close]').forEach(function (b) { b.addEventListener('click', function () { close(p); }); });
		if (p.getAttribute('data-auto') === 'true') {
			var key = 'popup-seen-' + p.id;
			try { if (!sessionStorage.getItem(key)) { sessionStorage.setItem(key, '1'); setTimeout(function () { open(p); }, 600); } } catch (e) { }
		}
	});
	document.addEventListener('click', function (e) {
		var a = e.target.closest && e.target.closest('a[href^="#popup-"]');
		if (!a) return;
		var p = document.getElementById(a.getAttribute('href').slice(1));
		if (p) { e.preventDefault(); open(p); }
	});
	document.addEventListener('keydown', function (e) {
		if (e.key !== 'Escape') return;
		popups.forEach(function (p) { if (!p.hidden && p.classList.contains('open')) close(p); });
	});
})();

// Scroll reveal: sections fade/rise into view (skipped for reduced motion or when the browser lacks IntersectionObserver)
(function () {
	var reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
	var targets = document.querySelectorAll(
		'.section-head, .about .copy, .about .photo, .partners-head, .logo-strip, .card, .fc, .ic-grid > div, .timeline .steps li, ' +
		'.faq-item, .dl-panel, .details, .cform, .center, .testimonials .copy, .contact h2');
	if (reduce || !('IntersectionObserver' in window) || !targets.length) { targets.forEach(function (t) { t.classList.add('in'); }); return; }
	var io = new IntersectionObserver(function (entries) {
		entries.forEach(function (e) { if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); } });
	}, { rootMargin: '0px 0px -8% 0px', threshold: 0.08 });
	targets.forEach(function (t) {
		t.classList.add('reveal');
		var idx = Array.prototype.indexOf.call(t.parentElement.children, t);
		t.style.setProperty('--d', Math.min(idx, 4) * 0.08 + 's');
		io.observe(t);
	});
})();
