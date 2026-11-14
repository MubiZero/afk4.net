// Оживление страницы: таймеры мест, вкладки с автопоказом, «Отключить интернет», быстрый переход Ctrl K, подсветка
// под курсором, появление блоков, копирование контактов. Без этого скрипта страница читается целиком; в лёгкой версии
// (слабая связь, экономия трафика, «меньше движения») остаются только сведения и действия, без украшений.
(function () {
  var root = document.documentElement;
  var lite = root.classList.contains('lite');
  var $ = function (selector, scope) { return (scope || document).querySelector(selector); };
  var $$ = function (selector, scope) { return [].slice.call((scope || document).querySelectorAll(selector)); };

  function pad(n) { return n < 10 ? '0' + n : String(n); }
  function clock(total) {
    var h = Math.floor(total / 3600), m = Math.floor((total % 3600) / 60), s = total % 60;
    return h > 0 ? h + ':' + pad(m) + ':' + pad(s) : pad(m) + ':' + pad(s);
  }

  // Таймеры идут и в лёгкой версии: это сведения, не украшение. Меньше 5 минут: место горит янтарём.
  var timers = $$('[data-seconds]').map(function (el) {
    var base = parseInt(el.getAttribute('data-seconds'), 10) || 0;
    return { el: el, seat: el.closest('.seat-play'), base: base, left: base };
  });
  function paint(t) {
    t.el.textContent = clock(t.left);
    if (t.seat) t.seat.classList.toggle('seat-warn', t.left > 0 && t.left < 300);
  }
  timers.forEach(paint);
  if (timers.length) {
    setInterval(function () {
      if (document.hidden) return;
      timers.forEach(function (t) {
        t.left = t.left > 0 ? t.left - 1 : t.base;
        paint(t);
      });
    }, 1000);
  }

  // Кнопка «Отключить интернет»: класс на сцене, подписи берём из data-атрибутов (язык страницы).
  var offline = $('[data-offline]');
  if (offline) {
    var toggle = $('[data-offline-toggle]', offline);
    var label = $('[data-offline-label]', offline);
    var state = $('[data-offline-state]', offline);
    toggle.addEventListener('click', function () {
      var off = offline.classList.toggle('is-off');
      toggle.setAttribute('aria-pressed', off ? 'true' : 'false');
      label.textContent = toggle.getAttribute(off ? 'data-label-on' : 'data-label-off');
      state.textContent = state.getAttribute(off ? 'data-text-off' : 'data-text-on');
    });
  }

  // Вкладки «что для вас важнее»: одна сцена в окне, бегунок под активной вкладкой, автопоказ с плавной полоской
  // (полоска — CSS-анимация, смена вкладки по её окончании; нажали сами — автопоказ навсегда останавливается).
  var tabs = $$('[data-tab]');
  var scenes = $$('[data-scene]');
  var pill = $('.tab-pill');
  var playing = false;
  var current = 0;
  function movePill(tab) {
    if (!pill || !tab) return;
    pill.style.width = tab.offsetWidth + 'px';
    pill.style.transform = 'translate(' + tab.offsetLeft + 'px,' + tab.offsetTop + 'px)';
  }
  function clearBars() { $$('.tab-bar').forEach(function (bar) { bar.remove(); }); }
  function show(index) {
    current = index;
    tabs.forEach(function (tab, i) {
      var on = i === index;
      tab.setAttribute('aria-selected', on ? 'true' : 'false');
      if (on) movePill(tab);
    });
    var key = tabs[index].getAttribute('data-tab');
    scenes.forEach(function (scene) { scene.hidden = scene.getAttribute('data-scene') !== key; });
  }
  function barOn(index) {
    clearBars();
    var bar = document.createElement('span');
    bar.className = 'tab-bar';
    bar.addEventListener('animationend', function () {
      if (!playing) return;
      show((current + 1) % tabs.length);
      barOn(current);
    });
    tabs[index].appendChild(bar);
  }
  if (tabs.length && scenes.length) {
    tabs.forEach(function (tab, index) {
      tab.addEventListener('click', function () { playing = false; clearBars(); show(index); });
    });
    show(0);
    window.addEventListener('resize', function () { movePill(tabs[current]); });
    var stage = $('.stage');
    if (!lite && stage && 'IntersectionObserver' in window && !window.matchMedia('(max-width: 700px)').matches) {
      playing = true;
      barOn(0);
      new IntersectionObserver(function (entries) { stage.classList.toggle('is-paused', !entries[0].isIntersecting); }, { threshold: 0.5 }).observe(stage);
      stage.addEventListener('pointerdown', function () { playing = false; clearBars(); });
      stage.addEventListener('keydown', function () { playing = false; clearBars(); });
    }
  }

  // Карта зала: нажмите на ПК, появятся команды; кнопка команды отвечает «команда отправлена» (пример, не боевая отправка).
  var panel = $('[data-seat-panel]');
  if (panel) {
    var seatName = $('[data-seat-name]', panel);
    var hint = $('[data-seat-hint]');
    var hintText = hint.textContent;
    var current = null;
    $$('[data-seat]').forEach(function (seat) {
      seat.addEventListener('click', function () {
        if (current) current.setAttribute('aria-pressed', 'false');
        current = seat;
        seat.setAttribute('aria-pressed', 'true');
        seatName.textContent = seat.getAttribute('data-seat');
        panel.classList.remove("is-idle");
        hint.textContent = hintText;
      });
    });
    $$('[data-act]', panel).forEach(function (button) {
      button.addEventListener('click', function () {
        hint.textContent = seatName.textContent + ': ' + button.getAttribute('data-act').toLowerCase() + ', ' + panel.getAttribute('data-sent');
      });
    });
  }

  // «Всё, что внутри»: плитки-значки слева, подробности выбранной группы справа; без скрипта подробности идут подряд.
  var atlasTiles = $$('[data-group]');
  var details = $$('[data-detail]');
  if (atlasTiles.length && details.length) {
    var pick = function (index, scroll) {
      atlasTiles.forEach(function (tile, i) { tile.setAttribute('aria-selected', i === index ? 'true' : 'false'); });
      details.forEach(function (detail, i) { detail.hidden = i !== index; });
      if (scroll && window.matchMedia('(max-width: 1000px)').matches) {
        var panelBox = $('.atlas-panel').getBoundingClientRect();
        if (panelBox.top < 0 || panelBox.bottom > window.innerHeight) $('.atlas-panel').scrollIntoView({ behavior: lite ? 'auto' : 'smooth', block: 'nearest' });
      }
    };
    atlasTiles.forEach(function (tile, index) {
      tile.addEventListener('click', function () { pick(index, true); });
      tile.addEventListener('keydown', function (event) {
        var next = event.key === 'ArrowRight' || event.key === 'ArrowDown' ? 1 : event.key === 'ArrowLeft' || event.key === 'ArrowUp' ? -1 : 0;
        if (!next) return;
        event.preventDefault();
        var target = (index + next + atlasTiles.length) % atlasTiles.length;
        atlasTiles[target].focus();
        pick(target, false);
      });
    });
    pick(0, false);
  }

  // Плитка «Три языка»: подсвечивает строки по очереди.
  var rows = $$('[data-lang-row]');
  if (rows.length) {
    var i = 0;
    var mark = function () {
      rows.forEach(function (row, index) { row.classList.toggle('is-active', index === i); });
      i = (i + 1) % rows.length;
    };
    mark();
    if (!lite) setInterval(mark, 2400);
  }

  // Выбор языка человеком запоминается: автоопределение больше не вмешивается (см. lite.js).
  $$('a[hreflang]').forEach(function (link) {
    link.addEventListener('click', function () {
      try { localStorage.setItem('afk4-lang', link.getAttribute('hreflang')); } catch (e) {}
    });
  });

  // Быстрый переход: Ctrl K или ⌘K, стрелки, Enter.
  var palette = $('[data-palette]');
  if (palette && palette.showModal) {
    var input = $('[data-palette-input]', palette);
    var items = $$('li', $('[data-palette-list]', palette));
    var empty = $('[data-palette-empty]', palette);
    var active = 0;
    var visibleItems = function () { return items.filter(function (li) { return !li.hidden; }); };
    var highlight = function () {
      var list = visibleItems();
      items.forEach(function (li) { $('a', li).classList.remove('is-active'); });
      if (list.length) { active = Math.max(0, Math.min(active, list.length - 1)); $('a', list[active]).classList.add('is-active'); }
      empty.hidden = list.length > 0;
    };
    var openPalette = function () { input.value = ''; items.forEach(function (li) { li.hidden = false; }); active = 0; palette.showModal(); highlight(); input.focus(); };
        document.addEventListener('keydown', function (event) {
      if ((event.ctrlKey || event.metaKey) && (event.key === 'k' || event.key === 'K' || event.code === 'KeyK')) {
        event.preventDefault();
        if (palette.open) palette.close(); else openPalette();
      }
    });
    input.addEventListener('input', function () {
      var query = input.value.trim().toLowerCase();
      items.forEach(function (li) { li.hidden = query !== '' && li.textContent.toLowerCase().indexOf(query) === -1; });
      active = 0;
      highlight();
    });
    input.addEventListener('keydown', function (event) {
      var list = visibleItems();
      if (event.key === 'ArrowDown') { event.preventDefault(); active = (active + 1) % Math.max(list.length, 1); highlight(); }
      else if (event.key === 'ArrowUp') { event.preventDefault(); active = (active - 1 + list.length) % Math.max(list.length, 1); highlight(); }
      else if (event.key === 'Enter' && list.length) { event.preventDefault(); $('a', list[active]).click(); }
    });
    palette.addEventListener('click', function (event) { if (event.target === palette) palette.close(); });
    $$('a', palette).forEach(function (link) { link.addEventListener('click', function () { palette.close(); }); });
  }

  // Украшения: только если человек не просил меньше движения и связь не слабая.
  if (!lite) {
    // Полоса прокрутки страницы.
    var bar = $('[data-progress]');
    if (bar) {
      var onScroll = function () {
        var max = document.documentElement.scrollHeight - window.innerHeight;
        bar.style.transform = 'scaleX(' + (max > 0 ? Math.min(window.scrollY / max, 1) : 0) + ')';
      };
      window.addEventListener('scroll', onScroll, { passive: true });
      onScroll();
    }

    // Появление блоков при прокрутке.
    var reveal = $$('[data-reveal]');
    if ('IntersectionObserver' in window) {
      var seen = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) { if (entry.isIntersecting) { entry.target.classList.add('is-in'); seen.unobserve(entry.target); } });
      }, { threshold: 0.12, rootMargin: '0px 0px -8% 0px' });
      reveal.forEach(function (el) { seen.observe(el); });
    } else {
      reveal.forEach(function (el) { el.classList.add('is-in'); });
    }

    // Подсветка под курсором у карточек.
    $$('.spot').forEach(function (el) {
      el.addEventListener('pointermove', function (event) {
        var rect = el.getBoundingClientRect();
        el.style.setProperty('--mx', (event.clientX - rect.left) + 'px');
        el.style.setProperty('--my', (event.clientY - rect.top) + 'px');
      });
    });

    // Герой: свет следует за курсором, Панель чуть поворачивается к нему.
    var hero = $('.hero');
    var shot = $('.hero-shot');
    if (hero && shot && window.matchMedia('(hover: hover)').matches) {
      var light = document.createElement('div');
      light.className = 'hero-spot';
      light.setAttribute('aria-hidden', 'true');
      hero.insertBefore(light, hero.firstChild);
      hero.addEventListener('pointermove', function (event) {
        var rect = hero.getBoundingClientRect();
        var x = (event.clientX - rect.left) / rect.width;
        var y = (event.clientY - rect.top) / rect.height;
        light.style.setProperty('--hx', (x * 100) + '%');
        light.style.setProperty('--hy', (y * 100) + '%');
        shot.style.transform = 'perspective(1800px) rotateX(' + (9 - (y - 0.5) * 3) + 'deg) rotateY(' + ((x - 0.5) * 4) + 'deg)';
      });
      hero.addEventListener('pointerleave', function () { shot.style.transform = ''; });
    }
  }

  // Форма связи (появится, когда у сайта будет адрес в API): отправка без перезагрузки.
  var form = $('[data-contact]');
  if (form && window.fetch) {
    var note = $('[data-form-state]', form);
    var submit = $('button[type="submit"]', form);
    form.addEventListener('submit', function (event) {
      event.preventDefault();
      note.className = 'form-state';
      note.textContent = form.getAttribute('data-sending');
      submit.disabled = true;
      var body = {};
      new FormData(form).forEach(function (value, key) { body[key] = String(value); });
      fetch(form.action, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
        .then(function (response) {
          if (!response.ok) throw new Error('status ' + response.status);
          note.className = 'form-state is-ok';
          note.textContent = form.getAttribute('data-sent');
          form.reset();
        })
        .catch(function () {
          note.className = 'form-state is-error';
          note.textContent = form.getAttribute('data-error');
        })
        .then(function () { submit.disabled = false; });
    });
  }
})();
