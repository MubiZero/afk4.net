import { GlobalRegistrator } from '@happy-dom/global-registrator';
import { registerTestDom } from '../../scripts/testing/testDom';

// Мост живёт в окне: ему нужны window.chrome, setTimeout и crypto.randomUUID.
registerTestDom(GlobalRegistrator, 'http://localhost/');
