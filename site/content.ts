// Тексты публичного сайта afk4.net (docs/operations/public-site.md). Три языка одной структурой: сборщик
// (build.ts) не даст собрать язык, где строки не хватает. Только правда о том, что программа уже умеет.

export type Lang = 'ru' | 'tg' | 'en';

export interface Feature {
  title: string;
  body: string;
}

export interface Content {
  htmlLang: string;
  langName: string;
  nav: { home: string; download: string; demo: string; signing: string; privacy: string };
  meta: { title: string; description: string };
  hero: { kicker: string; title: string; lead: string; download: string; demo: string; note: string; imageAlt: string };
  features: { title: string; items: Feature[] };
  player: { title: string; body: string; imageAlt: string };
  start: { title: string; steps: Feature[]; cta: string };
  open: { title: string; body: string; link: string };
  footer: { tagline: string; license: string; contact: string };
  download: {
    title: string;
    lead: string;
    button: string;
    buttonNote: string;
    requirementsTitle: string;
    requirements: string[];
    changesTitle: string;
    changesLead: string;
    changes: Feature[];
    dataTitle: string;
    dataBody: string;
    uninstallTitle: string;
    uninstallSteps: string[];
    uninstallLeftovers: string;
  };
  signing: {
    title: string;
    lead: string;
    attribution: string;
    whatTitle: string;
    whatBody: string;
    rolesTitle: string;
    roles: Feature[];
    processTitle: string;
    process: string[];
    privacyTitle: string;
    privacyBody: string;
  };
  privacy: {
    title: string;
    updated: string;
    sections: Feature[];
  };
}

const repo = 'https://github.com/MubiZero/afk4.net';

export const content: Record<Lang, Content> = {
  ru: {
    htmlLang: 'ru',
    langName: 'Рус',
    nav: { home: 'Главная', download: 'Скачать', demo: 'Демо', signing: 'Code signing policy', privacy: 'Конфиденциальность' },
    meta: {
      title: 'AFK4.NET: программа для компьютерного клуба',
      description: 'Управление ПК, сессиями, кассой и баром компьютерного клуба. Работает без интернета и после отключения света. Таджикский, русский, английский.'
    },
    hero: {
      kicker: 'Для компьютерных клубов Таджикистана',
      title: 'Клуб работает, даже когда пропал свет или интернет',
      lead: 'AFK4.NET ведёт ПК, сессии гостей, кассу и бар. Без интернета касса сажает гостей за наличные, а после отключения света гости получают своё время назад.',
      download: 'Скачать установщик',
      demo: 'Открыть демо Панели',
      note: 'Демо работает в браузере на выдуманном клубе, без регистрации.',
      imageAlt: 'Панель AFK4.NET: карта зала с ПК, оставшимся временем гостей и карточкой выбранного ПК'
    },
    features: {
      title: 'Что AFK4.NET делает за администратора',
      items: [
        { title: 'Свет пропал, время осталось', body: 'ПК замечают, что их обесточили. Когда так выключились несколько ПК разом, гостям возвращается время простоя, а владелец получает сообщение.' },
        { title: 'Касса без интернета', body: 'Нет связи с сервером: администратор всё равно сажает гостя за наличные, продлевает, продаёт товар и сдаёт смену. Когда связь вернётся, всё уйдёт на сервер без двойных списаний.' },
        { title: 'Игры качаются один раз', body: 'Кеш игр встроен в программу на ПК администратора: Steam и Epic берут обновление с него по сети клуба, а не из интернета на каждый ПК.' },
        { title: 'Здоровье ПК', body: 'Перегрев видеокарты, отказывающий диск, кончившееся место видны на карте зала. О критичном владелец узнаёт в Telegram раньше гостя.' },
        { title: 'Владелец видит всё с телефона', body: 'Итоги смены, расхождения кассы и сводка за день приходят в Telegram.' },
        { title: 'Три языка', body: 'Таджикский, русский и английский: у администратора в Панели и у гостя на экране ПК.' }
      ]
    },
    player: {
      title: 'Экран гостя',
      body: 'Вместо рабочего стола Windows гость видит экран клуба: оставшееся время, игры, новости клуба и кнопку вызова администратора. Диспетчер задач, «Выполнить» и редактор реестра закрыты.',
      imageAlt: 'Экран свободного ПК с новостью клуба'
    },
    start: {
      title: 'Как начать',
      steps: [
        { title: 'Установите', body: 'Один установщик на ПК администратора и на игровые ПК. Мастер спросит, какой это ПК.' },
        { title: 'Войдите по номеру', body: 'Владелец входит номером телефона и кодом из SMS. Сотрудников добавляет сам.' },
        { title: 'Работайте', body: 'ПК появляются на карте зала. Тарифы, бар и настройки ПК задаются в Панели.' }
      ],
      cta: 'Скачать установщик'
    },
    open: {
      title: 'Открытый исходный код',
      body: 'Код AFK4.NET открыт по лицензии AGPL-3.0: любой может проверить, что программа делает на ПК клуба.',
      link: 'Исходный код на GitHub'
    },
    footer: { tagline: 'Программа для компьютерных клубов', license: 'Исходный код: AGPL-3.0', contact: 'Связь' },
    download: {
      title: 'Скачать AFK4.NET',
      lead: 'Один установщик для всех ПК клуба: администратора и игровых. Мастер установки сам спросит, что это за ПК.',
      button: 'Скачать с GitHub',
      buttonNote: 'Последняя версия на странице выпусков GitHub. Программа обновляется сама.',
      requirementsTitle: 'Что нужно',
      requirements: ['Windows 10 или 11, 64 бита', 'Права администратора на время установки', 'Интернет для входа и обновлений. Без него ПК работают до конца оплаченного времени.'],
      changesTitle: 'Что установка меняет в Windows',
      changesLead: 'Чтобы ПК клуба работали как игровые места, программа меняет настройки Windows. Здесь всё, что она делает.',
      changes: [
        { title: 'Служба AFK4.NET Agent', body: 'Работает от имени системы, связывается с сервером клуба, открывает и закрывает ПК по сессиям, обновляет программу подписанными пакетами.' },
        { title: 'Киоск на игровых ПК', body: 'Создаётся учётная запись игрока с автовходом, вместо проводника запускается экран клуба. Диспетчер задач, «Выполнить», редактор реестра и выбранные клубом диски, флешки и сайты закрываются по настройкам клуба.' },
        { title: 'Правила брандмауэра', body: 'Открываются только для локальной сети клуба: TCP 47615 (команды стойки без интернета) и TCP 80 на ПК администратора (кеш игр).' },
        { title: 'Файл hosts', body: 'Пока работает кеш игр, в файл hosts добавляется блок между метками «AFK4 game cache», который ведёт загрузки Steam и Epic к кешу. Остальные строки не трогаются.' },
        { title: 'Включение по сети', body: '«Быстрый запуск» Windows выключается, сетевой карте разрешается будить ПК волшебным пакетом. Так клуб может включать ПК к открытию.' },
        { title: 'Выключение по простою и расписанию', body: 'Если клуб включил это в настройках, свободные ПК выключаются сами.' }
      ],
      dataTitle: 'Какие данные уходят на сервер',
      dataBody: 'Только на сервер AFK4.NET, с которым работает клуб: состояние ПК, сессии и оплаты, описание железа и его показатели (температуры, диски), список установленных программ. Подробно в политике конфиденциальности.',
      uninstallTitle: 'Как удалить',
      uninstallSteps: [
        'На игровом ПК запустите «Мастер установки AFK4.NET» и нажмите «Снять киоск»: учётная запись игрока удалится, вход в Windows станет прежним.',
        'Откройте «Параметры» → «Приложения» и удалите все программы с AFK4.NET в названии: AFK4.NET Agent, AFK4.NET Player Shell, Панель AFK4.NET, AFK4.NET и AFK4.NET Setup Payload.'
      ],
      uninstallLeftovers: 'После удаления могут остаться правила брандмауэра «AFK4.NET Agent (…)», блок «AFK4 game cache» в файле hosts и выключенный «быстрый запуск». Их убирают командами от имени администратора: netsh advfirewall firewall delete rule name="AFK4.NET Agent (club network)", то же для «(game cache)», а блок в C:\\Windows\\System32\\drivers\\etc\\hosts удаляется вручную. «Быстрый запуск» включается в «Электропитании».'
    },
    signing: {
      title: 'Code signing policy',
      lead: 'Как подписываются программы AFK4.NET и кто за это отвечает.',
      attribution: 'Free code signing provided by SignPath.io, certificate by SignPath Foundation.',
      whatTitle: 'Что подписывается',
      whatBody: 'Установщик AFK4.NET и пакеты MSI его частей (служба AFK4.NET Agent, экран игрока, Панель, мастер установки), собранные GitHub Actions из открытого репозитория. Чужие программы мы не подписываем.',
      rolesTitle: 'Кто отвечает',
      roles: [
        { title: 'Авторы и проверяющие', body: 'Участники репозитория с правом записи. Изменения со стороны проходят проверку участника перед слиянием.' },
        { title: 'Утверждающие', body: 'Владелец проекта. Каждый выпуск на подпись он утверждает вручную.' }
      ],
      processTitle: 'Как выпуск попадает на подпись',
      process: [
        'Каждое изменение проходит обязательные проверки CI; слить в main без зелёной проверки нельзя.',
        'Пакеты собирает GitHub Actions из исходного кода этого репозитория.',
        'Запрос на подпись утверждает владелец проекта вручную в SignPath.',
        'У всех участников включена двухфакторная проверка в GitHub и SignPath.'
      ],
      privacyTitle: 'Конфиденциальность',
      privacyBody: 'Программа передаёт данные только на сервер AFK4.NET, с которым работает клуб, и только для работы клуба. Что именно, описано в политике конфиденциальности.'
    },
    privacy: {
      title: 'Политика конфиденциальности',
      updated: 'Действует с 29.10.2026.',
      sections: [
        { title: 'Кто отвечает', body: 'Платформу AFK4.NET ведёт Мубинджон Мухамедов (Таджикистан). Вопросы о данных: postmaster@afk4.net.' },
        { title: 'Какие данные', body: 'Сотрудники клуба: имя, номер телефона, роль. Гости с учётной записью: номер телефона, имя, по желанию дата рождения, баланс и посещения. Сессии, оплаты, касса и склад клуба. С ПК клуба: имя компьютера, адрес в сети клуба, описание железа и его показатели (температуры, диски, место), список установленных программ, события защиты.' },
        { title: 'Зачем', body: 'Только чтобы клуб работал: открывать ПК по оплате, считать деньги, показывать владельцу отчёты и предупреждать о неисправных ПК. Данные не продаются и не передаются для рекламы.' },
        { title: 'Где хранятся', body: 'На серверах AFK4.NET. Резервные копии базы шифруются при хранении в Cloudflare R2 и хранятся 30 дней. SMS отправляет местный оператор, сообщения в Telegram уходят тем, кто сам подключил отчёты.' },
        { title: 'Этот сайт', body: 'Сайт afk4.net не ставит cookie, не подключает счётчики и сторонние шрифты.' },
        { title: 'Ваши права', body: 'Гость или сотрудник может попросить клуб или нас показать, исправить или удалить свои данные. Клуб может попросить удалить данные своей организации.' }
      ]
    }
  },
  tg: {
    htmlLang: 'tg',
    langName: 'Тоҷ',
    nav: { home: 'Асосӣ', download: 'Боргирӣ', demo: 'Демо', signing: 'Code signing policy', privacy: 'Махфият' },
    meta: {
      title: 'AFK4.NET: барнома барои клуби компютерӣ',
      description: 'Идоракунии ПК, сессияҳо, касса ва бари клуби компютерӣ. Бе интернет ва пас аз хомӯшии барқ кор мекунад. Тоҷикӣ, русӣ, англисӣ.'
    },
    hero: {
      kicker: 'Барои клубҳои компютерии Тоҷикистон',
      title: 'Клуб кор мекунад, ҳатто агар барқ ё интернет набошад',
      lead: 'AFK4.NET ПК-ҳо, сессияҳои меҳмонон, касса ва барро идора мекунад. Бе интернет касса меҳмонро бо пули нақд мешинонад, пас аз хомӯшии барқ меҳмонон вақти худро бармегардонанд.',
      download: 'Боргирии насбкунанда',
      demo: 'Кушодани демои Панел',
      note: 'Демо дар браузер бо клуби тахаюлӣ кор мекунад, бе бақайдгирӣ.',
      imageAlt: 'Панели AFK4.NET: харитаи толор бо ПК-ҳо, вақти боқимондаи меҳмонон ва кортаи ПК-и интихобшуда'
    },
    features: {
      title: 'AFK4.NET ба ҷои маъмур чӣ мекунад',
      items: [
        { title: 'Барқ рафт, вақт монд', body: 'ПК-ҳо мефаҳманд, ки онҳо бе барқ монданд. Вақте чанд ПК якбора хомӯш шуданд, ба меҳмонон вақти беистифода бармегардад ва соҳиб хабар мегирад.' },
        { title: 'Касса бе интернет', body: 'Алоқа бо сервер нест: маъмур ба ҳар ҳол меҳмонро бо пули нақд мешинонад, дароз мекунад, мол мефурӯшад ва ивазро месупорад. Вақте алоқа барқарор шуд, ҳама ба сервер бе дубора гирифтан меравад.' },
        { title: 'Бозиҳо як бор боргирӣ мешаванд', body: 'Кеши бозиҳо дар барномаи ПК-и маъмур сохта шудааст: Steam ва Epic навсозиро аз он тавассути шабакаи клуб мегиранд, на аз интернет барои ҳар ПК.' },
        { title: 'Саломатии ПК', body: 'Гармшавии видеокарта, диски вайроншаванда ва ҷои тамомшуда дар харитаи толор намоёнанд. Дар бораи ҳолати вазнин соҳиб дар Telegram пеш аз меҳмон огоҳ мешавад.' },
        { title: 'Соҳиб ҳамаро аз телефон мебинад', body: 'Натиҷаҳои иваз, фарқияти касса ва хулосаи рӯз ба Telegram меоянд.' },
        { title: 'Се забон', body: 'Тоҷикӣ, русӣ ва англисӣ: дар Панели маъмур ва дар экрани ПК-и меҳмон.' }
      ]
    },
    player: {
      title: 'Экрани меҳмон',
      body: 'Ба ҷои мизи кории Windows меҳмон экрани клубро мебинад: вақти боқимонда, бозиҳо, хабарҳои клуб ва тугмаи даъвати маъмур. Менеҷери вазифаҳо, «Иҷро» ва муҳаррири реестр пӯшидаанд.',
      imageAlt: 'Экрани ПК-и холӣ бо хабари клуб'
    },
    start: {
      title: 'Чӣ тавр оғоз кардан',
      steps: [
        { title: 'Насб кунед', body: 'Як насбкунанда барои ПК-и маъмур ва ПК-ҳои бозӣ. Устод мепурсад, ки ин кадом ПК аст.' },
        { title: 'Бо рақам ворид шавед', body: 'Соҳиб бо рақами телефон ва рамзи SMS ворид мешавад. Кормандонро худаш илова мекунад.' },
        { title: 'Кор кунед', body: 'ПК-ҳо дар харитаи толор пайдо мешаванд. Тарифҳо, бар ва танзимоти ПК дар Панел муқаррар мешаванд.' }
      ],
      cta: 'Боргирии насбкунанда'
    },
    open: {
      title: 'Рамзи манбаи кушода',
      body: 'Рамзи AFK4.NET бо иҷозатномаи AGPL-3.0 кушода аст: ҳар кас метавонад санҷад, ки барнома дар ПК-и клуб чӣ кор мекунад.',
      link: 'Рамзи манба дар GitHub'
    },
    footer: { tagline: 'Барнома барои клубҳои компютерӣ', license: 'Рамзи манба: AGPL-3.0', contact: 'Тамос' },
    download: {
      title: 'Боргирии AFK4.NET',
      lead: 'Як насбкунанда барои ҳамаи ПК-ҳои клуб: маъмур ва бозӣ. Устоди насб худаш мепурсад, ки ин кадом ПК аст.',
      button: 'Боргирӣ аз GitHub',
      buttonNote: 'Версияи охирин дар саҳифаи нашрҳои GitHub. Барнома худаш навсозӣ мешавад.',
      requirementsTitle: 'Чӣ лозим аст',
      requirements: ['Windows 10 ё 11, 64 бит', 'Ҳуқуқи маъмур ҳангоми насб', 'Интернет барои воридшавӣ ва навсозӣ. Бе он ПК-ҳо то охири вақти пардохтшуда кор мекунанд.'],
      changesTitle: 'Насб дар Windows чиро тағйир медиҳад',
      changesLead: 'Барои он ки ПК-ҳои клуб ҳамчун ҷойҳои бозӣ кор кунанд, барнома танзимоти Windows-ро тағйир медиҳад. Ин ҷо ҳамаи он чизест, ки вай мекунад.',
      changes: [
        { title: 'Хидмати AFK4.NET Agent', body: 'Аз номи система кор мекунад, бо сервери клуб пайваст мешавад, ПК-ро аз рӯи сессияҳо мекушояд ва мепӯшад, барномаро бо бастаҳои имзошуда навсозӣ мекунад.' },
        { title: 'Киоск дар ПК-ҳои бозӣ', body: 'Ҳисоби бозингар бо воридшавии худкор сохта мешавад, ба ҷои Explorer экрани клуб оғоз мешавад. Менеҷери вазифаҳо, «Иҷро», муҳаррири реестр ва дискҳо, флешкаҳо ва сайтҳои интихобкардаи клуб аз рӯи танзимоти клуб пӯшида мешаванд.' },
        { title: 'Қоидаҳои девори оташ', body: 'Танҳо барои шабакаи маҳаллии клуб кушода мешаванд: TCP 47615 (фармонҳои мизи кассир бе интернет) ва TCP 80 дар ПК-и маъмур (кеши бозиҳо).' },
        { title: 'Файли hosts', body: 'То кеши бозиҳо кор мекунад, ба файли hosts блок байни нишонаҳои «AFK4 game cache» илова мешавад, ки боргириҳои Steam ва Epic-ро ба кеш равона мекунад. Сатрҳои дигар дахл карда намешаванд.' },
        { title: 'Фурӯзон тавассути шабака', body: '«Оғози тез»-и Windows хомӯш мешавад, ба корти шабака иҷозат дода мешавад, ки ПК-ро бо бастаи ҷодугарӣ бедор кунад. Ҳамин тавр клуб метавонад ПК-ҳоро то кушодашавӣ фурӯзон кунад.' },
        { title: 'Хомӯшкунӣ аз рӯи бекорӣ ва ҷадвал', body: 'Агар клуб инро дар танзимот фаъол карда бошад, ПК-ҳои холӣ худашон хомӯш мешаванд.' }
      ],
      dataTitle: 'Кадом маълумот ба сервер меравад',
      dataBody: 'Танҳо ба сервери AFK4.NET, ки клуб бо он кор мекунад: ҳолати ПК, сессияҳо ва пардохтҳо, тавсифи сахтафзор ва нишондиҳандаҳои он (ҳарорат, дискҳо), рӯйхати барномаҳои насбшуда. Муфассал дар сиёсати махфият.',
      uninstallTitle: 'Чӣ тавр нест кардан',
      uninstallSteps: [
        'Дар ПК-и бозӣ «Устоди насби AFK4.NET»-ро оғоз кунед ва «Киоскро бардоштан»-ро пахш кунед: ҳисоби бозингар нест мешавад, воридшавӣ ба Windows ба ҳолати пешина бармегардад.',
        '«Танзимот» → «Барномаҳо»-ро кушоед ва ҳамаи барномаҳоеро, ки дар номашон AFK4.NET доранд, нест кунед: AFK4.NET Agent, AFK4.NET Player Shell, Панели AFK4.NET, AFK4.NET ва AFK4.NET Setup Payload.'
      ],
      uninstallLeftovers: 'Пас аз нест кардан метавонанд қоидаҳои девори оташи «AFK4.NET Agent (…)», блоки «AFK4 game cache» дар файли hosts ва «оғози тез»-и хомӯш монанд. Онҳоро бо фармонҳо аз номи маъмур нест мекунанд: netsh advfirewall firewall delete rule name="AFK4.NET Agent (club network)", ҳамин тавр барои «(game cache)», блок дар C:\\Windows\\System32\\drivers\\etc\\hosts дастӣ нест карда мешавад. «Оғози тез» дар «Таъминоти барқ» фаъол карда мешавад.'
    },
    signing: {
      title: 'Code signing policy',
      lead: 'Барномаҳои AFK4.NET чӣ тавр имзо карда мешаванд ва кӣ барои он ҷавобгар аст.',
      attribution: 'Free code signing provided by SignPath.io, certificate by SignPath Foundation.',
      whatTitle: 'Чӣ имзо карда мешавад',
      whatBody: 'Насбкунандаи AFK4.NET ва бастаҳои MSI-и қисмҳои он (хидмати AFK4.NET Agent, экрани бозингар, Панел, устоди насб), ки GitHub Actions аз анбори кушода месозад. Барномаҳои бегонаро мо имзо намекунем.',
      rolesTitle: 'Кӣ ҷавобгар аст',
      roles: [
        { title: 'Муаллифон ва санҷандагон', body: 'Иштирокчиёни анбор бо ҳуқуқи навиштан. Тағйироти берунӣ пеш аз муттаҳидшавӣ аз санҷиши иштирокчӣ мегузарад.' },
        { title: 'Тасдиқкунандагон', body: 'Соҳиби лоиҳа. Ҳар нашрро барои имзо ӯ дастӣ тасдиқ мекунад.' }
      ],
      processTitle: 'Нашр чӣ тавр ба имзо мерасад',
      process: [
        'Ҳар тағйир аз санҷишҳои ҳатмии CI мегузарад; бе санҷиши сабз ба main муттаҳид кардан мумкин нест.',
        'Бастаҳоро GitHub Actions аз рамзи манбаи ҳамин анбор месозад.',
        'Дархости имзоро соҳиби лоиҳа дастӣ дар SignPath тасдиқ мекунад.',
        'Ҳамаи иштирокчиён дар GitHub ва SignPath санҷиши дуомилиро фаъол кардаанд.'
      ],
      privacyTitle: 'Махфият',
      privacyBody: 'Барнома маълумотро танҳо ба сервери AFK4.NET, ки клуб бо он кор мекунад, ва танҳо барои кори клуб мефиристад. Чӣ маҳз, дар сиёсати махфият навишта шудааст.'
    },
    privacy: {
      title: 'Сиёсати махфият',
      updated: 'Аз 29.10.2026 амал мекунад.',
      sections: [
        { title: 'Кӣ ҷавобгар аст', body: 'Платформаи AFK4.NET-ро Мубинҷон Муҳамедов (Тоҷикистон) пеш мебарад. Саволҳо дар бораи маълумот: postmaster@afk4.net.' },
        { title: 'Кадом маълумот', body: 'Кормандони клуб: ном, рақами телефон, нақш. Меҳмонон бо ҳисоб: рақами телефон, ном, бо хоҳиш санаи таваллуд, баланс ва ташрифҳо. Сессияҳо, пардохтҳо, касса ва анбори клуб. Аз ПК-ҳои клуб: номи компютер, суроға дар шабакаи клуб, тавсифи сахтафзор ва нишондиҳандаҳои он (ҳарорат, дискҳо, ҷой), рӯйхати барномаҳои насбшуда, ҳодисаҳои ҳифз.' },
        { title: 'Барои чӣ', body: 'Танҳо барои кори клуб: кушодани ПК аз рӯи пардохт, ҳисоб кардани пул, нишон додани ҳисоботҳо ба соҳиб ва огоҳӣ дар бораи ПК-ҳои вайрон. Маълумот фурӯхта намешавад ва барои реклама дода намешавад.' },
        { title: 'Дар куҷо нигоҳ дошта мешавад', body: 'Дар серверҳои AFK4.NET. Нусхаҳои эҳтиётии базаи маълумот ҳангоми нигоҳдорӣ дар Cloudflare R2 рамзгузорӣ мешаванд ва 30 рӯз нигоҳ дошта мешаванд. SMS-ро оператори маҳаллӣ мефиристад, паёмҳо дар Telegram ба онҳое мераванд, ки худашон ҳисоботҳоро пайваст кардаанд.' },
        { title: 'Ин сайт', body: 'Сайти afk4.net cookie намегузорад, ҳисобгарҳо ва ҳарфҳои бегонаро пайваст намекунад.' },
        { title: 'Ҳуқуқҳои шумо', body: 'Меҳмон ё корманд метавонад аз клуб ё аз мо хоҳиш кунад, ки маълумоти ӯро нишон диҳад, ислоҳ кунад ё нест кунад. Клуб метавонад хоҳиш кунад, ки маълумоти ташкилоти ӯ нест карда шавад.' }
      ]
    }
  },
  en: {
    htmlLang: 'en',
    langName: 'Eng',
    nav: { home: 'Home', download: 'Download', demo: 'Demo', signing: 'Code signing policy', privacy: 'Privacy' },
    meta: {
      title: 'AFK4.NET: software for computer clubs',
      description: 'Run the PCs, guest sessions, cash desk and bar of a computer club. Keeps working offline and after a power cut. Tajik, Russian, English.'
    },
    hero: {
      kicker: 'For computer clubs in Tajikistan',
      title: 'The club keeps running when the power or internet goes out',
      lead: 'AFK4.NET runs the PCs, guest sessions, cash desk and bar. Offline, the desk still seats guests for cash; after a power cut, guests get their time back.',
      download: 'Download the installer',
      demo: 'Open the Panel demo',
      note: 'The demo runs in the browser on a made-up club, no sign-up.',
      imageAlt: 'AFK4.NET Panel: hall map with PCs, guests\u2019 remaining time and the selected PC card'
    },
    features: {
      title: 'What AFK4.NET does for the admin',
      items: [
        { title: 'Power cut, time kept', body: 'PCs notice they lost power. When several go off at once, guests get the downtime back and the owner gets a message.' },
        { title: 'Cash desk without internet', body: 'No connection to the server: the admin still seats guests for cash, extends, sells goods and closes the shift. When the connection returns, everything syncs without double charges.' },
        { title: 'Games download once', body: 'A game cache is built into the admin PC: Steam and Epic pull updates from it over the club network instead of the internet for every PC.' },
        { title: 'PC health', body: 'An overheating GPU, a failing disk or a full drive show on the hall map. The owner hears about critical problems in Telegram before a guest does.' },
        { title: 'The owner sees it all from a phone', body: 'Shift totals, cash discrepancies and a daily summary arrive in Telegram.' },
        { title: 'Three languages', body: 'Tajik, Russian and English, for the admin in the Panel and the guest on the PC screen.' }
      ]
    },
    player: {
      title: 'The guest screen',
      body: 'Instead of the Windows desktop the guest sees the club screen: remaining time, games, club news and a button to call the admin. Task Manager, Run and the registry editor are closed.',
      imageAlt: 'A free PC screen showing club news'
    },
    start: {
      title: 'Getting started',
      steps: [
        { title: 'Install', body: 'One installer for the admin PC and the gaming PCs. The wizard asks what kind of PC it is.' },
        { title: 'Sign in with a phone number', body: 'The owner signs in with a phone number and an SMS code, then adds the staff.' },
        { title: 'Run the club', body: 'PCs appear on the hall map. Rates, the bar and PC settings live in the Panel.' }
      ],
      cta: 'Download the installer'
    },
    open: {
      title: 'Open source',
      body: 'AFK4.NET is open source under AGPL-3.0, so anyone can check what it does on club PCs.',
      link: 'Source code on GitHub'
    },
    footer: { tagline: 'Software for computer clubs', license: 'Source code: AGPL-3.0', contact: 'Contact' },
    download: {
      title: 'Download AFK4.NET',
      lead: 'One installer for every PC in the club, admin and gaming. The setup wizard asks what kind of PC it is.',
      button: 'Download from GitHub',
      buttonNote: 'The latest version is on the GitHub releases page. The software updates itself.',
      requirementsTitle: 'Requirements',
      requirements: ['Windows 10 or 11, 64-bit', 'Administrator rights during setup', 'Internet for sign-in and updates. Without it, PCs keep running until the paid time ends.'],
      changesTitle: 'What setup changes in Windows',
      changesLead: 'To turn club PCs into gaming seats, the software changes Windows settings. This is everything it does.',
      changes: [
        { title: 'AFK4.NET Agent service', body: 'Runs as the system, talks to the club server, opens and closes the PC by session and updates the software with signed packages.' },
        { title: 'Kiosk on gaming PCs', body: 'A player account with automatic sign-in is created and the club screen replaces Explorer. Task Manager, Run, the registry editor and the drives, USB storage and websites the club picks are blocked per the club settings.' },
        { title: 'Firewall rules', body: 'Open only to the club\u2019s local network: TCP 47615 (desk commands without internet) and TCP 80 on the admin PC (game cache).' },
        { title: 'hosts file', body: 'While the game cache is on, a block between the \u201cAFK4 game cache\u201d markers is added to the hosts file to route Steam and Epic downloads to the cache. Other lines are left untouched.' },
        { title: 'Wake over the network', body: 'Windows Fast Startup is turned off and the network card may wake the PC with a magic packet, so the club can turn PCs on before opening.' },
        { title: 'Idle and scheduled shutdown', body: 'If the club enables it, free PCs turn themselves off.' }
      ],
      dataTitle: 'What data goes to the server',
      dataBody: 'Only to the AFK4.NET server the club uses: PC state, sessions and payments, a hardware description and its readings (temperatures, disks) and the list of installed programs. Details in the privacy policy.',
      uninstallTitle: 'How to uninstall',
      uninstallSteps: [
        'On a gaming PC, run the AFK4.NET setup wizard and choose \u201cRemove the kiosk\u201d: the player account is deleted and Windows sign-in goes back to how it was.',
        'Open Settings \u2192 Apps and uninstall every program with AFK4.NET in its name: AFK4.NET Agent, AFK4.NET Player Shell, the AFK4.NET Panel, AFK4.NET and AFK4.NET Setup Payload.'
      ],
      uninstallLeftovers: 'Firewall rules \u201cAFK4.NET Agent (\u2026)\u201d, the \u201cAFK4 game cache\u201d block in the hosts file and the disabled Fast Startup may remain. Remove them from an administrator prompt: netsh advfirewall firewall delete rule name="AFK4.NET Agent (club network)", the same for \u201c(game cache)\u201d, and delete the block in C:\\Windows\\System32\\drivers\\etc\\hosts by hand. Fast Startup is re-enabled in Power Options.'
    },
    signing: {
      title: 'Code signing policy',
      lead: 'How AFK4.NET software is signed and who is responsible.',
      attribution: 'Free code signing provided by SignPath.io, certificate by SignPath Foundation.',
      whatTitle: 'What is signed',
      whatBody: 'The AFK4.NET installer and the MSI packages of its parts (AFK4.NET Agent service, player screen, Panel, setup wizard), built by GitHub Actions from the open repository. We do not sign anyone else\u2019s software.',
      rolesTitle: 'Who is responsible',
      roles: [
        { title: 'Committers and reviewers', body: 'Repository members with write access. Outside contributions are reviewed by a member before merging.' },
        { title: 'Approvers', body: 'The project owner, who approves every release for signing by hand.' }
      ],
      processTitle: 'How a release gets signed',
      process: [
        'Every change passes required CI checks; nothing merges into main without a green run.',
        'GitHub Actions builds the packages from this repository\u2019s source code.',
        'The project owner approves each signing request by hand in SignPath.',
        'All members use multi-factor authentication on GitHub and SignPath.'
      ],
      privacyTitle: 'Privacy',
      privacyBody: 'The software sends data only to the AFK4.NET server the club uses, and only to run the club. The privacy policy lists exactly what.'
    },
    privacy: {
      title: 'Privacy policy',
      updated: 'Effective 29 October 2026.',
      sections: [
        { title: 'Who is responsible', body: 'The AFK4.NET platform is run by Mubinjon Mukhamedov (Tajikistan). Questions about data: postmaster@afk4.net.' },
        { title: 'What data', body: 'Club staff: name, phone number, role. Guests with an account: phone number, name, optional birth date, balance and visits. The club\u2019s sessions, payments, cash desk and stock. From club PCs: computer name, address on the club network, a hardware description and its readings (temperatures, disks, free space), the list of installed programs and protection events.' },
        { title: 'Why', body: 'Only to run the club: open PCs for paid time, keep the books, show the owner reports and warn about failing PCs. Data is not sold and not shared for advertising.' },
        { title: 'Where it is kept', body: 'On AFK4.NET servers. Database backups are encrypted at rest in Cloudflare R2 and kept for 30 days. SMS goes through a local operator; Telegram messages go only to people who connected reports themselves.' },
        { title: 'This website', body: 'afk4.net sets no cookies and loads no analytics or third-party fonts.' },
        { title: 'Your rights', body: 'A guest or staff member can ask the club or us to show, correct or delete their data. A club can ask for its organization\u2019s data to be deleted.' }
      ]
    }
  }
};

export const links = {
  repo,
  releases: `${repo}/releases/latest`,
  members: `${repo}/graphs/contributors`,
  owner: 'https://github.com/MubiZero',
  demo: 'https://demo.afk4.net',
  signpath: 'https://signpath.io',
  signpathFoundation: 'https://signpath.org',
  email: 'mailto:postmaster@afk4.net'
};
