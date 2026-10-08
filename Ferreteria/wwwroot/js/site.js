// =====================================================================
// El Maestro Online — interacciones y animaciones de la tienda
// =====================================================================
(function () {
    'use strict';

    const movimientoReducido = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const $ = (sel, ctx = document) => ctx.querySelector(sel);
    const $$ = (sel, ctx = document) => Array.from(ctx.querySelectorAll(sel));

    // ---------- Encabezado compacto, barra de progreso y botón "volver arriba" ----------
    const encabezado = $('.encabezado');
    const progreso = $('.progreso-scroll');
    const volverArriba = $('#volverArriba');
    let esperando = false;
    function alHacerScroll() {
        const y = window.scrollY;
        encabezado?.classList.toggle('compacto', y > 60);
        volverArriba?.classList.toggle('visible', y > 600);
        if (progreso) {
            const alto = document.documentElement.scrollHeight - window.innerHeight;
            progreso.style.transform = `scaleX(${alto > 0 ? y / alto : 0})`;
        }
        esperando = false;
    }
    window.addEventListener('scroll', () => {
        if (!esperando) { requestAnimationFrame(alHacerScroll); esperando = true; }
    }, { passive: true });
    alHacerScroll();
    volverArriba?.addEventListener('click', () => window.scrollTo({ top: 0, behavior: 'smooth' }));

    // ---------- Aparición de elementos al hacer scroll ----------
    const animables = $$('[data-anim]');
    if ('IntersectionObserver' in window && !movimientoReducido) {
        const observador = new IntersectionObserver(entradas => {
            entradas.forEach(e => {
                if (e.isIntersecting) { e.target.classList.add('visible'); observador.unobserve(e.target); }
            });
        }, { threshold: 0.12, rootMargin: '0px 0px -40px 0px' });
        animables.forEach(el => observador.observe(el));
    } else {
        animables.forEach(el => el.classList.add('visible'));
    }

    // ---------- Contadores animados (data-contar="1500") ----------
    function animarContador(el) {
        const meta = parseInt(el.dataset.contar, 10) || 0;
        const sufijo = el.dataset.sufijo || '';
        const duracion = 1600;
        const inicio = performance.now();
        const formato = new Intl.NumberFormat('es-CL');
        function paso(ahora) {
            const t = Math.min(1, (ahora - inicio) / duracion);
            const suave = 1 - Math.pow(1 - t, 3);
            el.textContent = formato.format(Math.round(meta * suave)) + sufijo;
            if (t < 1) requestAnimationFrame(paso);
        }
        requestAnimationFrame(paso);
    }
    const contadores = $$('[data-contar]');
    if ('IntersectionObserver' in window && !movimientoReducido) {
        const obs = new IntersectionObserver(entradas => {
            entradas.forEach(e => { if (e.isIntersecting) { animarContador(e.target); obs.unobserve(e.target); } });
        }, { threshold: 0.5 });
        contadores.forEach(c => obs.observe(c));
    } else {
        contadores.forEach(c => { c.textContent = new Intl.NumberFormat('es-CL').format(+c.dataset.contar) + (c.dataset.sufijo || ''); });
    }

    // ---------- Cuenta regresiva del Hot Sale (data-hasta="2026-10-11T23:59:59") ----------
    const relojes = $$('[data-hasta]');
    if (relojes.length) {
        const dos = n => String(n).padStart(2, '0');
        function actualizarRelojes() {
            relojes.forEach(r => {
                let resto = Math.max(0, new Date(r.dataset.hasta).getTime() - Date.now()) / 1000;
                const d = Math.floor(resto / 86400); resto -= d * 86400;
                const h = Math.floor(resto / 3600); resto -= h * 3600;
                const m = Math.floor(resto / 60);
                const s = Math.floor(resto - m * 60);
                r.querySelector('[data-d]').textContent = dos(d);
                r.querySelector('[data-h]').textContent = dos(h);
                r.querySelector('[data-m]').textContent = dos(m);
                r.querySelector('[data-s]').textContent = dos(s);
            });
        }
        actualizarRelojes();
        setInterval(actualizarRelojes, 1000);
    }

    // ---------- Carril horizontal de ofertas con flechas ----------
    $$('[data-carril]').forEach(contenedor => {
        const carril = $('.carril', contenedor);
        const desplazar = dir => carril.scrollBy({ left: dir * carril.clientWidth * 0.8, behavior: 'smooth' });
        $('[data-anterior]', contenedor)?.addEventListener('click', () => desplazar(-1));
        $('[data-siguiente]', contenedor)?.addEventListener('click', () => desplazar(1));
    });

    // ---------- Botón flotante de contacto ----------
    const contacto = $('#contactoFlotante');
    const botonContacto = $('#botonContacto');
    botonContacto?.addEventListener('click', () => {
        const abierto = contacto.classList.toggle('abierto');
        botonContacto.setAttribute('aria-expanded', abierto);
        $('#globoContacto')?.classList.add('oculto');
    });
    document.addEventListener('click', e => {
        if (contacto?.classList.contains('abierto') && !contacto.contains(e.target)) {
            contacto.classList.remove('abierto');
            botonContacto.setAttribute('aria-expanded', 'false');
        }
    });
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape') contacto?.classList.remove('abierto');
    });
    setTimeout(() => $('#globoContacto')?.classList.add('oculto'), 12000);

    // ---------- Avisos (toasts) que se cierran solos ----------
    $$('.avisos-flotantes .alert').forEach((aviso, i) => {
        setTimeout(() => {
            if (window.bootstrap) bootstrap.Alert.getOrCreateInstance(aviso).close();
        }, 5000 + i * 700);
    });
    // Si se acaba de agregar algo al carrito, el ícono "salta"
    if ($('.avisos-flotantes .alert-success')) $('#botonCarrito')?.classList.add('salto');

    // ---------- Imagen que vuela al carrito al agregar un producto ----------
    $$('form[data-agregar-carrito]').forEach(form => {
        form.addEventListener('submit', e => {
            if (movimientoReducido || form.dataset.enviando) return;
            const img = form.closest('[data-producto]')?.querySelector('img');
            const carrito = $('#botonCarrito');
            if (!img || !carrito || carrito.offsetParent === null) return;
            e.preventDefault();
            form.dataset.enviando = '1';
            const r1 = img.getBoundingClientRect();
            const r2 = carrito.getBoundingClientRect();
            const clon = img.cloneNode();
            clon.className = 'imagen-volando';
            Object.assign(clon.style, { left: r1.left + 'px', top: r1.top + 'px', width: r1.width + 'px', height: r1.height + 'px', borderRadius: '1rem' });
            document.body.appendChild(clon);
            requestAnimationFrame(() => {
                Object.assign(clon.style, {
                    left: (r2.left + r2.width / 2 - 15) + 'px', top: (r2.top + r2.height / 2 - 15) + 'px',
                    width: '30px', height: '30px', opacity: '.6', borderRadius: '50%'
                });
            });
            setTimeout(() => form.submit(), 650);
        });
    });

    // ---------- Selector de cantidad (+ / −) ----------
    $$('.selector-cantidad').forEach(sel => {
        const input = $('input', sel);
        const ajustar = d => {
            const min = +input.min || 1, max = +input.max || 99;
            input.value = Math.min(max, Math.max(min, (+input.value || min) + d));
        };
        $('[data-menos]', sel)?.addEventListener('click', () => ajustar(-1));
        $('[data-mas]', sel)?.addEventListener('click', () => ajustar(1));
    });

    // ---------- Zoom de la imagen en el detalle de producto ----------
    $$('.imagen-detalle[data-zoom]').forEach(caja => {
        const img = $('img', caja);
        caja.addEventListener('click', () => caja.classList.toggle('zoom'));
        caja.addEventListener('mousemove', e => {
            const r = caja.getBoundingClientRect();
            img.style.transformOrigin = `${((e.clientX - r.left) / r.width) * 100}% ${((e.clientY - r.top) / r.height) * 100}%`;
        });
        caja.addEventListener('mouseleave', () => caja.classList.remove('zoom'));
    });

    // ---------- Duración de la barra de progreso de los indicadores del slider ----------
    $$('.hero.carousel').forEach(c => c.style.setProperty('--intervalo', (c.dataset.bsInterval || 6000) + 'ms'));

    // ---------- Envío automático al cambiar un campo (orden del catálogo, cantidad del carrito) ----------
    $$('[data-autoenviar]').forEach(el => el.addEventListener('change', () => el.form?.submit()));

    // ---------- Boletín (ficticio) ----------
    $$('form[data-boletin]').forEach(f => f.addEventListener('submit', e => {
        e.preventDefault();
        f.innerHTML = '<div class="alert alert-success mb-0 fw-semibold">¡Listo, maestro! Te avisaremos de las próximas ofertas 🧡</div>';
    }));
})();
