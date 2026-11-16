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
