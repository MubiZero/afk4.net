// Тексты главной страницы afk4.net: бенто-доска, список возможностей и связь. Правило сайта
// (docs/operations/public-site.md): только то, что программа уже умеет; остальное — с меткой «В разработке».
// Опись и статусы: docs/operations/public-site-features.md. Таджикские строки перед выпуском читает носитель.
import type { Feature, Lang } from './content';

export interface Tile {
  title: string;
  text: string;
}

export interface Group {
  title: string;
  status: 'have' | 'planned';
  items: string[];
}

export interface Home {
  choose: { title: string; items: string[] };
  hero: { title: string; lead: string; nameLine: string; write: string; demo: string; demoNote: string; trust: string[] };
  board: {
    label: string;
    synthetic: string;
    have: string;
    planned: string;
    map: Tile & { free: string; left: string; offline: string; pc: string; hint: string; actions: { lock: string; unlock: string; message: string }; sent: string };
    till: Tile & { lines: { name: string; price: string }[]; total: string; currency: string };
    guest: Tile & { imageAlt: string };
    app: Tile & { booking: string; topUp: string };
    telegram: Tile & { messages: string[] };
    offline: Tile & { button: string; buttonBack: string; on: string; off: string; cashOnly: string; counting: string };
    cache: Tile & { done: string; pcs: string };
    langs: Tile & { phrases: { tg: string; ru: string; en: string } };
    soon: Tile & { items: string[] };
  };
  inside: { title: string; lead: string; groups: Group[] };
  how: { title: string; steps: Feature[]; cta: string };
  open: { title: string; body: string; link: string };
  contact: {
    title: string;
    lead: string;
    name: string;
    club: string;
    phone: string;
    message: string;
    send: string;
    sending: string;
    sent: string;
    error: string;
    consent: string;
    channels: string;
    telegram: string;
    call: string;
    email: string;
    emailNote: string;
    wait: string;
  };
}

export const home: Record<Lang, Home> = {
  ru: {
    choose: { title: 'Что для вас важнее всего?', items: ['Следить за залом', 'Принимать деньги и продавать в баре', 'Знать итоги, не приходя в клуб', 'Работать без интернета'] },
    hero: {
      title: 'Программа для игрового клуба',
      lead: 'ПК, гости, касса и бар в одном месте. Администратор видит весь зал, гость садится за ПК по номеру телефона, а вам в Telegram приходят итоги смены.',
      nameLine: 'Называется AFK4.NET. Сделана в Таджикистане: на таджикском, русском и английском.',
      write: 'Написать нам',
      demo: 'Открыть демо',
      demoNote: 'Демо открывается в браузере, на выдуманном клубе, без регистрации.',
      trust: ['Открытый исходный код', 'Что программа меняет в Windows', 'Демо без регистрации']
    },
    board: {
      label: 'Программа в работе',
      synthetic: 'Пример на выдуманном клубе',
      have: 'Есть в программе',
      planned: 'В разработке',
      map: {
        title: 'Весь зал на одной карте',
        text: 'Кто играет, сколько осталось, какой ПК свободен или не отвечает. Запереть, открыть, включить, написать гостю на экран: прямо с карты.',
        free: 'свободен',
        left: 'осталось',
        offline: 'нет связи',
        pc: 'ПК',
        hint: 'Нажмите на ПК: так выглядят команды в Панели.',
        actions: { lock: 'Запереть', unlock: 'Открыть', message: 'Написать гостю' },
        sent: 'команда отправлена'
      },
      till: {
        title: 'Касса и бар',
        text: 'Пополнение, чай и снеки, смена, чек. Деньги пишутся в журнал, который нельзя стереть: любое исправление видно.',
        lines: [
          { name: 'Чай', price: '6' },
          { name: 'Чипсы', price: '12' },
          { name: 'Время, 1 час', price: '15' }
        ],
        total: 'Итого',
        currency: 'сом.'
      },
      guest: {
        title: 'Экран гостя',
        text: 'Вместо рабочего стола Windows гость видит экран клуба: вход по номеру телефона или по QR, остаток времени, игры, бар и кнопка «позвать администратора».',
        imageAlt: 'Экран свободного ПК: номер места, новость клуба и системная панель с языком, звуком и сетью'
      },
      app: {
        title: 'Приложение игрока',
        text: 'Бронь ПК, пополнение, заказ в баре и друзья в телефоне. Войти на ПК можно, отсканировав QR.',
        booking: 'Бронь: ПК 07, сегодня 20:00',
        topUp: 'Пополнить'
      },
      telegram: {
        title: 'Итоги в Telegram',
        text: 'Смена закрыта, касса не сошлась, ПК перегревается. Сообщение приходит сразу, а не когда вы зайдёте в клуб.',
        messages: ['Смена закрыта. Касса сошлась: 1 240 сом.', 'ПК 04: видеокарта перегревается, 91 °C']
      },
      offline: {
        title: 'Нет интернета: клуб работает',
        text: 'Нажмите кнопку. ПК считают время дальше, администратор принимает наличные, а когда связь вернётся, всё уйдёт на сервер.',
        button: 'Отключить интернет',
        buttonBack: 'Вернуть интернет',
        on: 'Интернет есть',
        off: 'Интернета нет. Клуб работает.',
        cashOnly: 'Касса: только наличные',
        counting: 'Время идёт'
      },
      cache: {
        title: 'Игры качаются один раз',
        text: 'Кеш игр встроен в ПК администратора: обновления Steam и Epic берутся из сети клуба, а не из интернета на каждый ПК.',
        done: 'Скачано из интернета один раз',
        pcs: 'Остальные ПК берут из сети клуба'
      },
      langs: {
        title: 'Три языка',
        text: 'Таджикский, русский и английский: у администратора в Панели и у гостя на экране ПК.',
        phrases: { tg: 'Ба клуб хуш омадед', ru: 'Добро пожаловать в клуб', en: 'Welcome to the club' }
      },
      soon: {
        title: 'Что ещё готовим',
        text: 'В разработке, сроков не обещаем.',
        items: ['Заказ снеков и напитков с телефона', 'Один аккаунт гостя во всех клубах сети']
      }
    },
    inside: {
      title: 'Всё, что внутри',
      lead: 'Полный список. Метка «Есть в программе» значит, что функция уже сделана и её можно посмотреть в демо. «В разработке» — ещё нет.',
      groups: [
        {
          title: 'Карта зала и ПК',
          status: 'have',
          items: [
            'Залы и места, цвет и остаток времени у каждого ПК',
            'Команды: запереть, открыть, перезагрузить, выключить, включить по сети',
            'Сообщение на экран гостя и выход из его сессии',
            'Несколько ПК выбираются сразу, действие одно',
            'Режим обслуживания: ПК открыт для персонала и не продаётся гостям',
            'Места без ПК: приставки и ТВ ведутся вручную, как обычные места',
            'Здоровье ПК: температура видеокарты, диски, место, тревога владельцу'
          ]
        },
        {
          title: 'Сессии и тарифы',
          status: 'have',
          items: [
            'Старт, продление, перенос на другой ПК, пауза, конец',
            'Тарифы можно менять: цена уже начатой сессии не изменится',
            'Пакеты времени, бонусы и кешбэк',
            'Долг «оплатит потом»',
            'Гость сам начинает сессию на ПК: входит, выбирает время, платит с баланса'
          ]
        },
        {
          title: 'Касса и бар',
          status: 'have',
          items: [
            'Товары, категории, остатки, штрихкод, предупреждение о малом остатке',
            'Продажа, возврат, отмена: исходная запись не стирается',
            'Смена: открытие, закрытие, сверка наличных, расхождение',
            'Чеки и чаевые администратору',
            'Большие денежные действия проходят только с подтверждением'
          ]
        },
        {
          title: 'Гости',
          status: 'have',
          items: [
            'Список гостей, поиск, объединение дублей',
            'Брони, отзывы и оценка визита',
            'Подарок на день рождения',
            'Новости и события клуба на экранах ПК и в Панели',
            'Турниры'
          ]
        },
        {
          title: 'Экран гостя на ПК',
          status: 'have',
          items: [
            'Вместо Windows экран клуба: ни рабочего стола, ни панели задач',
            'Вход по телефону и ПИН или по QR',
            'Выбор времени, продление, пополнение',
            'Игры, бар, «позвать администратора»',
            'Итог визита: оценка и чаевые',
            'Язык, раскладка, звук и сеть в системной панели',
            'Гость не залезет в настройки Windows: диспетчер задач, «Выполнить» и реестр закрыты, флешки и сайты по выбору клуба тоже'
          ]
        },
        {
          title: 'Владелец',
          status: 'have',
          items: [
            'Роли и права: владелец, управляющий, старший смены, администратор, техник, бухгалтер',
            'Журнал «События»: кто, что и когда сделал',
            'Отчёты: Сводка, Смены и касса, Выручка',
            'Несколько филиалов',
            'Итоги смены, тревоги кассы и сводка за вчера в Telegram',
            'Перенос гостей с балансами из другой программы'
          ]
        },
        {
          title: 'Когда что-то пошло не так',
          status: 'have',
          items: [
            'Касса без интернета: наличные, продление, смена',
            'Пропал свет: гостям возвращается время простоя',
            'Тревога о неисправном ПК раньше, чем заметит гость',
            'Копия базы каждый час',
            'Обновления приходят сами, скачивается только изменённое'
          ]
        },
        {
          title: 'Подключение',
          status: 'have',
          items: [
            'Один установщик на все ПК клуба, мастер сам спросит, что это за ПК',
            'Владелец входит по номеру телефона и коду из SMS',
            'Демо Панели без регистрации',
            'Таджикский, русский и английский'
          ]
        },
        {
          title: 'Приложение игрока',
          status: 'planned',
          items: [
            'Каталог клубов и бронь',
            'Пополнение, пакеты, кешбэк',
            'Друзья, приглашения, турниры',
            'Вход на ПК по QR'
          ]
        },
        {
          title: 'Один аккаунт на сеть клубов',
          status: 'planned',
          items: [
            'Телефон, ПИН и репутация общие для клубов сети',
            'Деньги у каждого клуба свои',
            'Гость пришёл в другой клуб сети: его уже знают'
          ]
        },
        {
          title: 'Заказ в баре с телефона',
          status: 'planned',
          items: ['Гость выбирает снеки и напитки в приложении', 'У администратора очередь заказов']
        },
        {
          title: 'Раздача игр по кешу',
          status: 'planned',
          items: [
            'Кеш встроен в ПК администратора',
            'Обновления Steam и Epic берутся из сети клуба',
            'Проверка на живом ПК клуба впереди'
          ]
        }
      ]
    },
    how: {
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
    contact: {
      title: 'Расскажите про ваш клуб',
      lead: 'Сколько у вас ПК, какая программа сейчас и чего не хватает. Мы покажем AFK4.NET в деле и ответим на вопросы.',
      name: 'Как к вам обращаться',
      club: 'Клуб и город',
      phone: 'Телефон или Telegram',
      message: 'Что важно для вашего клуба',
      send: 'Отправить',
      sending: 'Отправляем…',
      sent: 'Сообщение ушло. Мы ответим вам сами.',
      error: 'Не получилось отправить. Напишите в Telegram или позвоните, контакты рядом.',
      consent: 'Мы прочитаем сообщение и ответим вам, больше ничего с ним не делаем.',
      channels: 'Или сразу',
      telegram: 'Telegram',
      call: 'Позвонить',
      email: 'Почта',
      emailNote: 'Читаем её реже, ответ придёт не сразу',
      wait: 'Отвечаем лично'
    }
  },
  tg: {
    choose: { title: 'Барои шумо чӣ муҳимтар аст?', items: ['Толорро назорат кардан', 'Пул қабул кардан ва дар бар фурӯхтан', 'Натиҷаро бе омадан ба клуб донистан', 'Бе интернет кор кардан'] },
    hero: {
      title: 'Барнома барои клуби бозӣ',
      lead: 'ПК-ҳо, меҳмонон, касса ва бар дар як ҷой. Маъмур тамоми толорро мебинад, меҳмон бо рақами телефон ба ПК менишинад, ва ба Telegram-и шумо натиҷаҳои иваз меояд.',
      nameLine: 'Номаш AFK4.NET. Дар Тоҷикистон сохта шудааст: бо забонҳои тоҷикӣ, русӣ ва англисӣ.',
      write: 'Ба мо нависед',
      demo: 'Кушодани демо',
      demoNote: 'Демо дар браузер бо клуби тахаюлӣ кушода мешавад, бе бақайдгирӣ.',
      trust: ['Рамзи манбаи кушода', 'Барнома дар Windows чиро тағйир медиҳад', 'Демо бе бақайдгирӣ']
    },
    board: {
      label: 'Барнома дар кор',
      synthetic: 'Намуна дар клуби тахаюлӣ',
      have: 'Дар барнома ҳаст',
      planned: 'Дар кор',
      map: {
        title: 'Тамоми толор дар як харита',
        text: 'Кӣ бозӣ мекунад, чӣ қадар монд, кадом ПК озод аст ё ҷавоб намедиҳад. Пӯшидан, кушодан, фурӯзон кардан, ба экрани меҳмон навиштан: аз худи харита.',
        free: 'озод',
        left: 'монд',
        offline: 'алоқа нест',
        pc: 'ПК',
        hint: 'ПК-ро пахш кунед: фармонҳо дар Панел чунин менамоянд.',
        actions: { lock: 'Пӯшидан', unlock: 'Кушодан', message: 'Ба меҳмон навиштан' },
        sent: 'фармон фиристода шуд'
      },
      till: {
        title: 'Касса ва бар',
        text: 'Пур кардан, чой ва газак, иваз, чек. Пул ба маҷаллае навишта мешавад, ки нест намешавад: ҳар ислоҳ намоён аст.',
        lines: [
          { name: 'Чой', price: '6' },
          { name: 'Чипс', price: '12' },
          { name: 'Вақт, 1 соат', price: '15' }
        ],
        total: 'Ҷамъ',
        currency: 'сом.'
      },
      guest: {
        title: 'Экрани меҳмон',
        text: 'Ба ҷои мизи кории Windows меҳмон экрани клубро мебинад: воридшавӣ бо рақами телефон ё QR, вақти боқимонда, бозиҳо, бар ва тугмаи «даъват кардани маъмур».',
        imageAlt: 'Экрани ПК-и холӣ: рақами ҷой, хабари клуб ва панели система бо забон, садо ва шабака'
      },
      app: {
        title: 'Барномаи бозингар',
        text: 'Брони ПК, пур кардан, фармоиш дар бар ва дӯстон дар телефон. Ба ПК бо скан кардани QR ворид шудан мумкин аст.',
        booking: 'Брон: ПК 07, имрӯз 20:00',
        topUp: 'Пур кардан'
      },
      telegram: {
        title: 'Натиҷаҳо дар Telegram',
        text: 'Иваз пӯшида шуд, касса ҷамъ наомад, ПК аз ҳад гарм шуд. Паём фавран меояд, на вақте ки шумо ба клуб медароед.',
        messages: ['Иваз пӯшида шуд. Касса ҷамъ омад: 1 240 сом.', 'ПК 04: видеокарта аз ҳад гарм мешавад, 91 °C']
      },
      offline: {
        title: 'Интернет нест: клуб кор мекунад',
        text: 'Тугмаро пахш кунед. ПК-ҳо вақтро давом медиҳанд, маъмур пули нақдро қабул мекунад, ва вақте алоқа барқарор шуд, ҳама ба сервер меравад.',
        button: 'Интернетро хомӯш кардан',
        buttonBack: 'Интернетро баргардонидан',
        on: 'Интернет ҳаст',
        off: 'Интернет нест. Клуб кор мекунад.',
        cashOnly: 'Касса: танҳо пули нақд',
        counting: 'Вақт меравад'
      },
      cache: {
        title: 'Бозиҳо як бор боргирӣ мешаванд',
        text: 'Кеши бозиҳо дар ПК-и маъмур сохта шудааст: навсозии Steam ва Epic аз шабакаи клуб гирифта мешавад, на аз интернет барои ҳар ПК.',
        done: 'Як бор аз интернет боргирӣ шуд',
        pcs: 'ПК-ҳои дигар аз шабакаи клуб мегиранд'
      },
      langs: {
        title: 'Се забон',
        text: 'Тоҷикӣ, русӣ ва англисӣ: дар Панели маъмур ва дар экрани ПК-и меҳмон.',
        phrases: { tg: 'Ба клуб хуш омадед', ru: 'Добро пожаловать в клуб', en: 'Welcome to the club' }
      },
      soon: {
        title: 'Боз чӣ тайёр мекунем',
        text: 'Дар кор аст, мӯҳлат ваъда намекунем.',
        items: ['Фармоиши газак ва нӯшокӣ аз телефон', 'Як ҳисоби меҳмон дар ҳамаи клубҳои шабака']
      }
    },
    inside: {
      title: 'Ҳама чизи дарун',
      lead: 'Рӯйхати пурра. Нишони «Дар барнома ҳаст» маънои онро дорад, ки имконият аллакай сохта шудааст ва онро дар демо дидан мумкин аст. «Дар кор» — ҳанӯз не.',
      groups: [
        {
          title: 'Харитаи толор ва ПК',
          status: 'have',
          items: [
            'Толорҳо ва ҷойҳо, ранг ва вақти боқимонда дар ҳар ПК',
            'Фармонҳо: пӯшидан, кушодан, аз нав боргирӣ, хомӯш кардан, бо шабака фурӯзон кардан',
            'Паём ба экрани меҳмон ва баровардани ӯ аз сессия',
            'Якчанд ПК якбора интихоб мешавад, амал як аст',
            'Ҳолати хизматрасонӣ: ПК барои кормандон кушода аст ва ба меҳмон фурӯхта намешавад',
            'Ҷойҳо бе ПК: приставкаҳо ва ТВ дастӣ идора мешаванд, мисли ҷойҳои оддӣ',
            'Саломатии ПК: ҳарорати видеокарта, дискҳо, ҷой, ҳушдор ба соҳиб'
          ]
        },
        {
          title: 'Сессияҳо ва тарифҳо',
          status: 'have',
          items: [
            'Оғоз, дароз кардан, гузаронидан ба ПК-и дигар, таваққуф, анҷом',
            'Тарифҳоро тағйир додан мумкин аст: нархи сессияи оғозшуда тағйир намеёбад',
            'Бастаҳои вақт, бонусҳо ва кэшбэк',
            'Қарзи «баъдтар месупорад»',
            'Меҳмон худаш сессияро дар ПК оғоз мекунад: ворид мешавад, вақтро интихоб мекунад, аз баланс месупорад'
          ]
        },
        {
          title: 'Касса ва бар',
          status: 'have',
          items: [
            'Молҳо, категорияҳо, бақияҳо, штрихкод, огоҳӣ дар бораи бақияи кам',
            'Фурӯш, баргардонидан, бекор кардан: сабти аслӣ нест намешавад',
            'Иваз: кушодан, пӯшидан, муқоисаи пули нақд, фарқият',
            'Чекҳо ва чойпулӣ ба маъмур',
            'Амалҳои калони пулӣ танҳо бо тасдиқ мегузаранд'
          ]
        },
        {
          title: 'Меҳмонон',
          status: 'have',
          items: [
            'Рӯйхати меҳмонон, ҷустуҷӯ, якҷоя кардани такрорҳо',
            'Бронҳо, тафсирҳо ва баҳои ташриф',
            'Тӯҳфа ба рӯзи таваллуд',
            'Хабарҳо ва рӯйдодҳои клуб дар экрани ПК ва дар Панел',
            'Мусобиқаҳо'
          ]
        },
        {
          title: 'Экрани меҳмон дар ПК',
          status: 'have',
          items: [
            'Ба ҷои Windows экрани клуб: на мизи корӣ, на панели вазифаҳо',
            'Воридшавӣ бо телефон ва ПИН ё бо QR',
            'Интихоби вақт, дароз кардан, пур кардан',
            'Бозиҳо, бар, «даъват кардани маъмур»',
            'Хулосаи ташриф: баҳо ва чойпулӣ',
            'Забон, тарҳбандӣ, садо ва шабака дар панели система',
            'Меҳмон ба танзимоти Windows намедарояд: менеҷери вазифаҳо, «Иҷро» ва реестр пӯшидаанд, флешкаҳо ва сайтҳо аз рӯи интихоби клуб низ'
          ]
        },
        {
          title: 'Соҳиб',
          status: 'have',
          items: [
            'Нақшҳо ва ҳуқуқҳо: соҳиб, мудир, сардори навбат, маъмур, техник, муҳосиб',
            'Маҷаллаи «Рӯйдодҳо»: кӣ, чӣ ва кай кард',
            'Ҳисоботҳо: Хулоса, Иваз ва касса, Даромад',
            'Якчанд филиал',
            'Натиҷаҳои иваз, ҳушдорҳои касса ва хулосаи дирӯза дар Telegram',
            'Гузаронидани меҳмонон бо балансҳо аз барномаи дигар'
          ]
        },
        {
          title: 'Вақте чизе хато меравад',
          status: 'have',
          items: [
            'Касса бе интернет: пули нақд, дароз кардан, иваз',
            'Барқ рафт: ба меҳмонон вақти беистифода бармегардад',
            'Ҳушдор дар бораи ПК-и вайроншуда пеш аз он ки меҳмон бинад',
            'Нусхаи пойгоҳи додаҳо ҳар соат',
            'Навсозиҳо худашон меоянд, танҳо қисми тағйирёфта боргирӣ мешавад'
          ]
        },
        {
          title: 'Пайвастшавӣ',
          status: 'have',
          items: [
            'Як насбкунанда барои ҳамаи ПК-ҳои клуб, устод худаш мепурсад, ки ин кадом ПК аст',
            'Соҳиб бо рақами телефон ва рамзи SMS ворид мешавад',
            'Демои Панел бе бақайдгирӣ',
            'Тоҷикӣ, русӣ ва англисӣ'
          ]
        },
        {
          title: 'Барномаи бозингар',
          status: 'planned',
          items: [
            'Феҳристи клубҳо ва брон',
            'Пур кардан, баста, кэшбэк',
            'Дӯстон, даъватҳо, мусобиқаҳо',
            'Воридшавӣ ба ПК бо QR'
          ]
        },
        {
          title: 'Як ҳисоб барои шабакаи клубҳо',
          status: 'planned',
          items: [
            'Телефон, ПИН ва обрӯ барои клубҳои шабака умумӣ аст',
            'Пул дар ҳар клуб алоҳида аст',
            'Меҳмон ба клуби дигари шабака омад: ӯро аллакай мешиносанд'
          ]
        },
        {
          title: 'Фармоиш дар бар аз телефон',
          status: 'planned',
          items: ['Меҳмон газак ва нӯшокиро дар барнома интихоб мекунад', 'Маъмур навбати фармоишҳоро мебинад']
        },
        {
          title: 'Пахши бозиҳо тавассути кеш',
          status: 'planned',
          items: [
            'Кеш дар ПК-и маъмур сохта шудааст',
            'Навсозии Steam ва Epic аз шабакаи клуб гирифта мешавад',
            'Санҷиш дар ПК-и зиндаи клуб пеш аст'
          ]
        }
      ]
    },
    how: {
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
    contact: {
      title: 'Дар бораи клуби худ нависед',
      lead: 'Чанд ПК доред, айни замон кадом барнома истифода мешавад ва чӣ намерасад. Мо AFK4.NET-ро дар амал нишон медиҳем ва ба саволҳо ҷавоб мегӯем.',
      name: 'Чӣ тавр муроҷиат кунем',
      club: 'Клуб ва шаҳр',
      phone: 'Телефон ё Telegram',
      message: 'Барои клуби шумо чӣ муҳим аст',
      send: 'Фиристодан',
      sending: 'Фиристода истодаем…',
      sent: 'Паём рафт. Мо худамон ҷавоб медиҳем.',
      error: 'Фиристодан нашуд. Дар Telegram нависед ё занг занед, тамосҳо дар паҳлӯ.',
      consent: 'Мо паёмро мехонем ва ба шумо ҷавоб медиҳем, бо он чизи дигаре намекунем.',
      channels: 'Ё якбора',
      telegram: 'Telegram',
      call: 'Занг задан',
      email: 'Почта',
      emailNote: 'Онро камтар мехонем, ҷавоб фавран намеояд',
      wait: 'Худамон ҷавоб медиҳем'
    }
  },
  en: {
    choose: { title: 'What matters most to you?', items: ['Keep an eye on the hall', 'Take money and sell at the bar', 'Know the results without being at the club', 'Work without internet'] },
    hero: {
      title: 'Software for a gaming club',
      lead: 'PCs, guests, till and bar in one place. Your admin sees the whole hall, a guest sits down at a PC with a phone number, and you get the shift results in Telegram.',
      nameLine: 'It is called AFK4.NET. Made in Tajikistan, in Tajik, Russian and English.',
      write: 'Write to us',
      demo: 'Open the demo',
      demoNote: 'The demo opens in your browser on an invented club, no sign-up.',
      trust: ['Open source code', 'What it changes in Windows', 'Demo without sign-up']
    },
    board: {
      label: 'The program at work',
      synthetic: 'Example on an invented club',
      have: 'In the program',
      planned: 'In development',
      map: {
        title: 'The whole hall on one map',
        text: 'Who is playing, how much time is left, which PC is free or not answering. Lock, unlock, wake, message the guest on their screen: right from the map.',
        free: 'free',
        left: 'left',
        offline: 'no link',
        pc: 'PC',
        hint: 'Press a PC: this is how commands look in the Panel.',
        actions: { lock: 'Lock', unlock: 'Unlock', message: 'Message the guest' },
        sent: 'command sent'
      },
      till: {
        title: 'Till and bar',
        text: 'Top-ups, tea and snacks, shifts, receipts. Money goes into a log that cannot be erased: every correction is visible.',
        lines: [
          { name: 'Tea', price: '6' },
          { name: 'Chips', price: '12' },
          { name: 'Time, 1 hour', price: '15' }
        ],
        total: 'Total',
        currency: 'TJS'
      },
      guest: {
        title: 'Guest screen',
        text: 'Instead of the Windows desktop, the guest sees the club screen: sign in by phone number or QR, time left, games, bar and a “call the admin” button.',
        imageAlt: 'A free PC screen: seat number, a club news item and a system bar with language, sound and network'
      },
      app: {
        title: 'Player app',
        text: 'Book a PC, top up, order at the bar and keep friends in your phone. Sign in at a PC by scanning a QR.',
        booking: 'Booking: PC 07, today 20:00',
        topUp: 'Top up'
      },
      telegram: {
        title: 'Results in Telegram',
        text: 'Shift closed, till does not match, a PC is overheating. The message arrives at once, not when you next visit the club.',
        messages: ['Shift closed. Till matches: 1,240 TJS.', 'PC 04: graphics card overheating, 91 °C']
      },
      offline: {
        title: 'No internet: the club keeps working',
        text: 'Press the button. PCs keep counting time, the admin takes cash, and when the link returns everything goes to the server.',
        button: 'Cut the internet',
        buttonBack: 'Restore the internet',
        on: 'Internet is up',
        off: 'No internet. The club keeps working.',
        cashOnly: 'Till: cash only',
        counting: 'Time is running'
      },
      cache: {
        title: 'Games download once',
        text: 'The game cache is built into the admin PC: Steam and Epic updates come from the club network, not from the internet on every PC.',
        done: 'Downloaded from the internet once',
        pcs: 'The other PCs take it from the club network'
      },
      langs: {
        title: 'Three languages',
        text: 'Tajik, Russian and English: for the admin in the Panel and for the guest on the PC screen.',
        phrases: { tg: 'Ба клуб хуш омадед', ru: 'Добро пожаловать в клуб', en: 'Welcome to the club' }
      },
      soon: {
        title: 'What we are building next',
        text: 'In development, no dates promised.',
        items: ['Order snacks and drinks from a phone', 'One guest account across all clubs of a network']
      }
    },
    inside: {
      title: 'Everything inside',
      lead: 'The full list. “In the program” means the feature is built and you can see it in the demo. “In development” means not yet.',
      groups: [
        {
          title: 'Floor map and PCs',
          status: 'have',
          items: [
            'Halls and seats, colour and time left on every PC',
            'Commands: lock, unlock, restart, shut down, wake over the network',
            'A message on the guest’s screen and sign-out of their session',
            'Select several PCs at once, one action',
            'Maintenance mode: the PC is open for staff and not sold to guests',
            'Seats without a PC: consoles and TVs are run by hand like any seat',
            'PC health: graphics card temperature, disks, space, an alert to the owner'
          ]
        },
        {
          title: 'Sessions and tariffs',
          status: 'have',
          items: [
            'Start, extend, move to another PC, pause, end',
            'Tariffs can change: the price of a session already started does not',
            'Time packages, bonuses and cashback',
            'Debt: “will pay later”',
            'A guest starts their own session at the PC: signs in, picks time, pays from the balance'
          ]
        },
        {
          title: 'Till and bar',
          status: 'have',
          items: [
            'Products, categories, stock, barcode, low-stock warning',
            'Sale, return, void: the original record is never erased',
            'Shift: open, close, cash count, mismatch',
            'Receipts and tips for the admin',
            'Large money actions only go through with a confirmation'
          ]
        },
        {
          title: 'Guests',
          status: 'have',
          items: [
            'Guest list, search, merging duplicates',
            'Bookings, reviews and a rating for the visit',
            'Birthday gift',
            'Club news and events on PC screens and in the Panel',
            'Tournaments'
          ]
        },
        {
          title: 'Guest screen on the PC',
          status: 'have',
          items: [
            'The club screen instead of Windows: no desktop, no taskbar',
            'Sign in by phone and PIN or by QR',
            'Pick time, extend, top up',
            'Games, bar, “call the admin”',
            'Visit summary: rating and tips',
            'Language, keyboard layout, sound and network in a system bar',
            'A guest cannot get into Windows settings: Task Manager, Run and the registry are blocked, and so are the USB drives and sites the club picks'
          ]
        },
        {
          title: 'Owner',
          status: 'have',
          items: [
            'Roles and permissions: owner, manager, shift lead, admin, technician, accountant',
            'The “Events” log: who did what and when',
            'Reports: Summary, Shifts and till, Revenue',
            'Several branches',
            'Shift results, till alerts and yesterday’s summary in Telegram',
            'Move guests with their balances from another program'
          ]
        },
        {
          title: 'When something goes wrong',
          status: 'have',
          items: [
            'Till without internet: cash, extensions, shifts',
            'Power cut: guests get their downtime back',
            'An alert about a failing PC before a guest notices',
            'A database copy every hour',
            'Updates arrive by themselves, only what changed is downloaded'
          ]
        },
        {
          title: 'Getting connected',
          status: 'have',
          items: [
            'One installer for every PC in the club, the wizard asks what kind of PC it is',
            'The owner signs in with a phone number and an SMS code',
            'A Panel demo without sign-up',
            'Tajik, Russian and English'
          ]
        },
        {
          title: 'Player app',
          status: 'planned',
          items: ['Club catalogue and booking', 'Top-up, packages, cashback', 'Friends, invitations, tournaments', 'Sign in at a PC by QR']
        },
        {
          title: 'One account across a club network',
          status: 'planned',
          items: [
            'Phone, PIN and reputation shared across the network’s clubs',
            'Each club keeps its own money',
            'A guest walks into another club of the network: they are already known'
          ]
        },
        {
          title: 'Ordering at the bar from a phone',
          status: 'planned',
          items: ['A guest picks snacks and drinks in the app', 'The admin gets an order queue']
        },
        {
          title: 'Game distribution through the cache',
          status: 'planned',
          items: [
            'The cache is built into the admin PC',
            'Steam and Epic updates come from the club network',
            'A test on a real club PC is still ahead'
          ]
        }
      ]
    },
    how: {
      title: 'How to start',
      steps: [
        { title: 'Install', body: 'One installer for the admin PC and for the gaming PCs. The wizard asks what kind of PC it is.' },
        { title: 'Sign in by phone', body: 'The owner signs in with a phone number and an SMS code and adds staff themselves.' },
        { title: 'Work', body: 'PCs appear on the floor map. Tariffs, bar and PC settings are set in the Panel.' }
      ],
      cta: 'Download the installer'
    },
    open: {
      title: 'Open source code',
      body: 'AFK4.NET’s code is open under AGPL-3.0: anyone can check what the program does on a club’s PCs.',
      link: 'Source code on GitHub'
    },
    contact: {
      title: 'Tell us about your club',
      lead: 'How many PCs you have, what program you use now and what is missing. We will show you AFK4.NET at work and answer your questions.',
      name: 'What should we call you',
      club: 'Club and city',
      phone: 'Phone or Telegram',
      message: 'What matters for your club',
      send: 'Send',
      sending: 'Sending…',
      sent: 'Message sent. We will answer you personally.',
      error: 'Could not send. Write to us on Telegram or call, the contacts are next to this form.',
      consent: 'We will read the message and reply, and do nothing else with it.',
      channels: 'Or right away',
      telegram: 'Telegram',
      call: 'Call',
      email: 'Email',
      emailNote: 'We read it less often, so the reply is not instant',
      wait: 'We reply personally'
    }
  }
};

export const contacts = {
  telegram: 'https://t.me/MubiDev',
  telegramHandle: '@MubiDev',
  phone: 'tel:+992186663333',
  phoneShown: '+992 18 666 33 33'
};
