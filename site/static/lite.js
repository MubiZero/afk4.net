// Первым в head: (1) лёгкая версия при слабой связи, экономии трафика или просьбе системы «меньше движения»;
// (2) язык по умолчанию. Русская страница при первом заходе сама выбирает язык посетителя: сохранённый выбор;
// таджикский язык браузера даёт таджикский; страны СНГ (по часовому поясу и языку браузера) остаются на русском;
// остальной мир получает английский. Страну по IP сайт не узнаёт (статика), поэтому часовой пояс: он ничего не передаёт.
(function () {
  var root = document.documentElement;
  try {
    var c = navigator.connection || {};
    var slow = c.saveData === true || /(^|-)2g$/.test(c.effectiveType || '');
    var calm = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (slow || calm) root.classList.add('lite');
  } catch (e) {}
  root.classList.add('js');

  try {
    if (root.lang !== 'ru') return;
    var target = null;
    var saved = localStorage.getItem('afk4-lang');
    if (saved === 'tg' || saved === 'en' || saved === 'ru') {
      target = saved;
    } else {
      var langs = (navigator.languages && navigator.languages.length ? navigator.languages : [navigator.language || '']).map(function (l) { return String(l).toLowerCase().slice(0, 2); });
      var zone = '';
      try { zone = Intl.DateTimeFormat().resolvedOptions().timeZone || ''; } catch (e) {}
      var cisZone = /^(Europe\/(Moscow|Kaliningrad|Samara|Volgograd|Kirov|Astrakhan|Saratov|Ulyanovsk|Simferopol|Minsk|Kiev|Kyiv|Chisinau)|Asia\/(Dushanbe|Tashkent|Samarkand|Almaty|Qyzylorda|Qostanay|Aqtobe|Aqtau|Atyrau|Oral|Bishkek|Ashgabat|Yerevan|Baku|Tbilisi|Yekaterinburg|Omsk|Novosibirsk|Barnaul|Tomsk|Novokuznetsk|Krasnoyarsk|Irkutsk|Chita|Yakutsk|Khandyga|Vladivostok|Ust-Nera|Magadan|Sakhalin|Srednekolymsk|Kamchatka|Anadyr))$/.test(zone);
      var cisLang = ['ru', 'be', 'uk', 'kk', 'ky', 'uz', 'tk', 'az', 'hy', 'ka'].indexOf(langs[0]) !== -1;
      if (langs[0] === 'tg') target = 'tg';
      else if (cisZone || cisLang) target = 'ru';
      else target = 'en';
    }
    if (target && target !== 'ru') {
      var alt = document.querySelector('link[rel="alternate"][hreflang="' + target + '"]');
      if (alt) location.replace(new URL(alt.href).pathname + location.search + location.hash);
    }
  } catch (e) {}
})();
