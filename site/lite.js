// Лёгкая версия: слабая связь, экономия трафика или просьба системы «меньше движения».
(function () {
  try {
    var c = navigator.connection || {};
    var slow = c.saveData === true || /(^|-)2g$/.test(c.effectiveType || '');
    var calm = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (slow || calm) document.documentElement.classList.add('lite');
  } catch (e) {}
  document.documentElement.classList.add('js');
})();
