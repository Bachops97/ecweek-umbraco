// Mobile menu + dropdowns
(function () {
	var header = document.getElementById('siteHeader');
	var toggle = document.getElementById('navToggle');
	if (!header || !toggle) return;

	toggle.addEventListener('click', function () {
		var open = header.classList.toggle('nav-open');
		toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
		document.body.classList.toggle('no-scroll', open);
	});

	// Sub menus: click toggles (mobile). On desktop they open on hover via CSS.
	header.querySelectorAll('.has-sub > .sub-toggle').forEach(function (btn) {
		btn.addEventListener('click', function () {
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
				items.forEach(function (it) { it.classList.toggle('hide', cat && it.getAttribute('data-cat') !== cat); });
			});
		});
	});
})();

// Popups: open from links to #popup-<id> (or automatically once per visit); close with X, backdrop or Esc
(function () {
	var popups = document.querySelectorAll('[data-popup]');
	if (!popups.length) return;
	var lastFocus = null;
	function open(p) { lastFocus = document.activeElement; p.hidden = false; document.body.classList.add('no-scroll'); var c = p.querySelector('.popup-close'); if (c) c.focus(); }
	function close(p) { p.hidden = true; document.body.classList.remove('no-scroll'); if (lastFocus && lastFocus.focus) lastFocus.focus(); }
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
		popups.forEach(function (p) { if (!p.hidden) close(p); });
	});
})();
