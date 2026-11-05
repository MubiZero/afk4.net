import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { I18nProvider } from '@afk4/i18n';
import { App } from './App';
import { loadIntlPolyfills } from '@afk4/formatting/intl-polyfills';
import './styles.css';

// Miniblink знает в Intl только en — русские даты и числа приходят полифилами до первого кадра.
void loadIntlPolyfills().then(() => createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <I18nProvider>
      <App />
    </I18nProvider>
  </StrictMode>
));
