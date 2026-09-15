import { GlobalRegistrator } from '@happy-dom/global-registrator';

// Мост живёт в окне: ему нужны window.chrome, setTimeout и crypto.randomUUID.
GlobalRegistrator.register({ url: 'http://localhost/' });
