// Оживление главной: таймеры мест, «Отключить интернет», смена языка в плитке, форма связи.
// Без этого скрипта страница читается целиком; скрипт только добавляет движение и отправку формы.
(function () {
  var lite = document.documentElement.classList.contains('lite');

  function pad(n) { return n < 10 ? '0' + n : String(n); }
  function clock(total) {
    var h = Math.floor(total / 3600), m = Math.floor((total % 3600) / 60), s = total % 60;
    return h > 0 ? h + ':' + pad(m) + ':' + pad(s) : pad(m) + ':' + pad(s);
  }

  // Таймеры идут и в лёгкой версии: это сведения, не украшение.
  var timers = [].map.call(document.querySelectorAll('[data-seconds]'), function (el) {
    var base = parseInt(el.getAttribute('data-seconds'), 10) || 0;
    return { el: el, base: base, left: base };
  });
  if (timers.length) {
    setInterval(function () {
      if (document.hidden) return;
      timers.forEach(function (t) {
        t.left = t.left > 0 ? t.left - 1 : t.base;
        t.el.textContent = clock(t.left);
      });
    }, 1000);
  }

  // Кнопка «Отключить интернет»: класс на плитке, подписи берём из data-атрибутов (язык страницы).
  var tile = document.querySelector('[data-offline]');
  if (tile) {
    var button = tile.querySelector('[data-offline-toggle]');
    var label = tile.querySelector('[data-offline-label]');
    var state = tile.querySelector('[data-offline-state]');
    button.addEventListener('click', function () {
      var off = tile.classList.toggle('is-off');
      button.setAttribute('aria-pressed', off ? 'true' : 'false');
      label.textContent = button.getAttribute(off ? 'data-label-on' : 'data-label-off');
      state.textContent = state.getAttribute(off ? 'data-text-off' : 'data-text-on');
    });
  }

  // «Что для вас важнее»: выбранная плитка встаёт на место главной, прежняя главная уходит на её место.
  var board = document.querySelector('[data-board]');
  var choices = document.querySelectorAll('[data-choose]');
  if (board && choices.length) {
    [].forEach.call(choices, function (choice) {
      choice.addEventListener('click', function () {
        var key = choice.getAttribute('data-choose');
        var on = choice.getAttribute('aria-pressed') !== 'true';
        [].forEach.call(choices, function (other) { other.setAttribute('aria-pressed', 'false'); });
        if (on) { choice.setAttribute('aria-pressed', 'true'); board.setAttribute('data-focus', key); }
        else { board.removeAttribute('data-focus'); }
      });
    });
  }

  // Карта зала: нажмите на ПК, появятся команды; кнопка команды отвечает «команда отправлена» (это пример, не боевая отправка).
  var panel = document.querySelector('[data-seat-panel]');
  if (panel) {
    var seatName = panel.querySelector('[data-seat-name]');
    var hint = document.querySelector('[data-seat-hint]');
    var hintText = hint.textContent;
    var current = null;
    [].forEach.call(document.querySelectorAll('[data-seat]'), function (seat) {
      seat.addEventListener('click', function () {
        if (current) current.setAttribute('aria-pressed', 'false');
        current = seat;
        seat.setAttribute('aria-pressed', 'true');
        seatName.textContent = seat.getAttribute('data-seat');
        panel.hidden = false;
        hint.textContent = hintText;
      });
    });
    [].forEach.call(panel.querySelectorAll('[data-act]'), function (button) {
      button.addEventListener('click', function () {
        hint.textContent = seatName.textContent + ': ' + button.getAttribute('data-act').toLowerCase() + ', ' + panel.getAttribute('data-sent');
      });
    });
  }

  // Плитка «Три языка»: подсвечивает строки по очереди.
  var rows = document.querySelectorAll('[data-lang-row]');
  if (rows.length) {
    var i = 0;
    var mark = function () {
      [].forEach.call(rows, function (row, index) { row.classList.toggle('is-active', index === i); });
      i = (i + 1) % rows.length;
    };
    mark();
    if (!lite) setInterval(mark, 2400);
  }

  // Список «Всё, что внутри»: на узком экране группы свёрнуты, на широком открыты.
  if (window.matchMedia && window.matchMedia('(max-width: 700px)').matches) {
    [].forEach.call(document.querySelectorAll('.group'), function (group) { group.removeAttribute('open'); });
  }

  // Форма связи: отправка без перезагрузки; без скрипта форма уходит обычным POST.
  var form = document.querySelector('[data-contact]');
  if (form && window.fetch) {
    var note = form.querySelector('[data-form-state]');
    var submit = form.querySelector('button[type="submit"]');
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
