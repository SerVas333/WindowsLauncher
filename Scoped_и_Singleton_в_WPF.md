
# Scoped и Singleton в WPF: как правильно применять lifetimes

В .NET DI есть три базовые «времени жизни» сервисов: Transient, Scoped и Singleton; WPF-приложения не имеют встроенного «per-request» scope как в ASP.NET, поэтому важно явно определить, что считать scope и как создавать/завершать его в настольном приложении. Веб-контекст: Scoped создаётся на запрос и утилизируется в конце запроса; Singleton живёт до завершения приложения; Transient создаётся при каждом запросе к контейнеру.[^1_1][^1_2][^1_3]

## Что означают lifetimes

- Singleton: один экземпляр на всё приложение; должен быть потокобезопасным; освобождается при остановке ServiceProvider.[^1_1]
- Scoped: один экземпляр на «область» (scope); не должен потребляться из Singleton напрямую; корректный доступ — через явное создание IServiceScope и разрешение зависимостей внутри него.[^1_2][^1_1]
- Transient: новый экземпляр при каждом запросе; освобождается в конце текущего scope.[^1_4][^1_1]

> Критично избегать «captive dependency»: Singleton, держащий ссылку на Scoped, — ошибка конфигурации и приводит к некорректному состоянию; включайте проверку scope’ов в dev (validateScopes: true).[^1_2]

## Особенность WPF: что считать «scope»

В WPF нет автоматического «per-request» scope, как в ASP.NET, поэтому scope нужно определять вручную — по окну, по View/ViewModel, по бизнес-операции или временному «единичному действию пользователя». Распространённые варианты:[^1_5][^1_4]

- Scope на окно или View/ViewModel: создаётся при открытии окна/вью модели, утилизируется при закрытии; все scoped-сервисы внутри живут столько же, transient — освобождаются вместе со scope.[^1_5][^1_4]
- Scope на бизнес-операцию/команду: создать IServiceScope на время выполнения use case и освободить по завершении, чтобы корректно управлять IDisposable зависимостями.[^1_4][^1_1]

Microsoft явно рекомендует: scoped-сервисы всегда использовать внутри scope (явно создаваемого через IServiceScopeFactory.CreateScope()), а не вытаскивать их из Singleton; Singleton может разрешать scoped, только создавая собственный временный scope.[^1_1][^1_2]

## Практические рекомендации для WPF

- Что регистрировать как Singleton: конфигурации, кэш, логгеры, навигация уровня приложения, шины событий уровня приложения, фабрики, которые потокобезопасны и не содержат пользовательского состояния.[^1_1]
- Что регистрировать как Scoped: контексты данных/юнит работы на окно или на операцию; «разделяемое состояние» внутри окна; мапперы/репозитории, которые должны жить в пределах окна/действия.[^1_4][^1_1]
- Что регистрировать как Transient: лёгкие статeless-сервисы, хелперы, форматтеры, короткоживущие обработчики и т.д..[^1_3][^1_6][^1_1]


## EF Core DbContext в WPF

В ASP.NET AddDbContext по умолчанию регистрирует DbContext как Scoped, что логично «на запрос»; в WPF «запроса» нет, поэтому привяжите DbContext к вручную определённому scope (например, на окно/операцию), либо создавайте единичные контексты на использование через фабрику. Варианты, встречающиеся на практике:[^1_3][^1_4][^1_1]

- Использовать AddDbContext (scoped) и гарантировать, что резолв и работа с DbContext происходят в явно созданном IServiceScope, соответствующем окну/действию.[^1_4][^1_1]
- Использовать фабрику DbContext (Singleton-фабрика, создающая короткоживущие DbContext) и оборачивать их в using, утилизируя сразу после операции.[^1_7]

Важно: не «протаскивать» DbContext как scoped в Singleton и не хранить его дольше выбранного scope.[^1_7][^1_2][^1_1]

## Как создать scopes в WPF

- Создайте корневой ServiceProvider в App при старте приложения.
- Когда открывается окно/создаётся ViewModel, создайте IServiceScope через ServiceProvider.CreateScope(), резолвьте зависимости из scope.ServiceProvider и храните scope до закрытия окна/завершения операции; затем вызовите Dispose у scope (можно в закрытии окна/IDisposable ViewModel).[^1_1][^1_4]
- Не резолвьте scoped-сервисы из Singleton-конструкторов; если нужно — внутри метода Singleton создавайте временный scope.[^1_2][^1_1]

Примерная модель из практики: «каждое View/ViewModel — свой scope», при закрытии View — Dispose scope; так автоматически освобождаются transient и scoped IDisposable-сервисы, тогда как Singleton остаются до завершения приложения.[^1_5][^1_4]

## Частые ошибки и как их избежать

- Captive dependency: Singleton зависит от Scoped — избегать, включить валидацию scope’ов в dev.[^1_2]
- Отсутствие Disposal у scopes в WPF — приводит к утечкам; всегда освобождать IServiceScope, когда окно/операция завершены.[^1_4]
- Использование Scoped без scope в десктопе — фактически превращается в Singleton, что ломает семантику и состояние.[^1_2]


## Краткая памятка

- Singleton — для глобально разделяемых, потокобезопасных, длинноживущих сервисов.[^1_1]
- Scoped — «на окно» или «на действие», с явным CreateScope/Dispose; не потреблять из Singleton напрямую.[^1_2][^1_4][^1_1]
- Transient — для лёгких, stateless компонентов; освобождаются в конце текущего scope.[^1_4][^1_1]

Если требуется более детальный шаблон кода для App.xaml.cs и создания scopes на окно/ViewModel, могу привести минимальный пример.

[^1_3][^1_7][^1_5][^1_1][^1_2][^1_4]

<div style="text-align: center">⁂</div>

[^1_1]: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection

[^1_2]: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines

[^1_3]: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-usage

[^1_4]: https://csharp.christiannagel.com/2018/08/29/scopes/

[^1_5]: https://ru.stackoverflow.com/questions/1280009/Как-объявлять-скоп-лайфтайма-если-контейнер-di-не-должен-покидать-корень-композ

[^1_6]: https://www.bytehide.com/blog/scoped-transient-singleton-csharp

[^1_7]: https://stackoverflow.com/questions/61957461/lifetime-of-scoped-ef-core-dbcontext-in-wpf

[^1_8]: https://metanit.com/sharp/aspnet5/6.5.php

[^1_9]: https://www.reddit.com/r/dotnet/comments/15m8vib/how_to_work_with_scoped_service_in_wpf/?tl=ru

[^1_10]: https://www.reddit.com/r/csharp/comments/1acwtar/can_someone_explain_when_to_use_singleton_scoped/?tl=ru

[^1_11]: https://www.reddit.com/r/csharp/comments/1acwtar/can_someone_explain_when_to_use_singleton_scoped/

[^1_12]: https://stackoverflow.com/questions/52774336/service-lifetimes-transient-vs-scoped-vs-singleton

[^1_13]: https://www.youtube.com/watch?v=sSq3GtriFuM

[^1_14]: https://autofac.readthedocs.io/en/latest/faq/per-request-scope.html

[^1_15]: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/application-management-overview

[^1_16]: https://learn.microsoft.com/en-us/answers/questions/672441/how-to-select-the-lifetime-scope-for-dependencies

[^1_17]: https://qna.habr.com/q/1340640

[^1_18]: https://www.c-sharpcorner.com/article/microsoft-extensions-dependencyinjection-for-dependency-injection/

[^1_19]: https://github.com/jbogard/MediatR/issues/667

[^1_20]: https://ci-sharp.ru/obuchenie/oshibki-i-ih-resheniya/kakov-pravilnyi-sposob-sozdaniia-odnoekzempliarnogo-prilozheniia-wpf-v-c/


---

# Ошибка “Cannot consume scoped service from singleton”: что это и как исправить

Сообщение означает, что сервис с более длинным временем жизни (**Singleton**) получает зависимость с более коротким временем жизни (**Scoped**), что нарушает правила DI и вызывает исключение при валидации скоупов или приводит к скрытым сбоям во время работы. Правильное решение — не инжектировать scoped напрямую в singleton, а создавать краткоживущий скоуп при каждом использовании scoped-зависимости через IServiceScopeFactory/IServiceProvider.[^2_1][^2_2][^2_3]

## Почему так нельзя

Scoped-сервисы рассчитаны на работу внутри ограниченного жизненного цикла (в вебе — «на запрос»), тогда как Singleton живёт столько, сколько живёт корневой контейнер; если Singleton удерживает scoped, то последний фактически становится долгоживущим «пленником» (captive dependency), что ломает семантику времени жизни, приводит к использованию уже освобождённых ресурсов и ошибкам потокобезопасности. В среде разработки включённая проверка скоупов обычно сразу бросает InvalidOperationException с тем самым текстом ошибки, хотя без валидации проблема остаётся, просто проявляется не сразу.[^2_4][^2_5][^2_2][^2_6]

## Правильный паттерн с IServiceScope

Когда Singleton вынужден работать с сервисом, который зарегистрирован как Scoped (например, DbContext, UserManager или любой «на запрос»), требуется создавать новый скоуп на время операции и получать зависимость из него, после чего корректно освобождать скоуп. Это делается либо через внедрённый IServiceScopeFactory, либо через IServiceProvider.CreateScope(); оба варианта эквивалентны по сути.[^2_7][^2_8][^2_1]

### Минимальный пример

Вместо того чтобы инжектировать IMyScopedService в конструктор Singleton, создаётся скоуп в момент вызова метода Singleton и из него извлекается зависимость, после чего скоуп уничтожается по завершении блока using:[^2_8][^2_1]

```csharp
public class MySingleton
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MySingleton(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void DoWork()
    {
        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IMyScopedService>();
        svc.Handle(); // работа строго внутри скоупа
    }
}
```


## Частые сценарии и нюансы

В фоновых задачах и HostedService (зарегистрированы как Singleton) попытка инжектировать scoped-зависимости приводит к той же ошибке; корректный подход — создавать скоуп внутри ExecuteAsync/обработчика таймера и получать DbContext/репозитории из этого скоупа. В middleware scoped-зависимости нельзя инжектировать в конструктор, но их можно получать как параметры метода Invoke/InvokeAsync — тогда их предоставит текущий запросный скоуп. Если «в Dev падает, а в Prod нет», это значит, что в Dev включена валидация скоупов; проблему всё равно нужно исправлять, иначе поведение останется некорректным и в продакшене.[^2_6][^2_3][^2_9][^2_10][^2_4][^2_1][^2_7]

## Диагностика и защита

Для раннего выявления ошибок времени жизни включается проверка скоупов и валидация при сборке провайдера, что гарантированно выдаст исключение при неверной конфигурации lifetimes:[^2_2][^2_6]

```csharp
builder.Host.UseDefaultServiceProvider((_, options) =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});
```


## Краткая памятка

Singleton не должен держать ссылки на Scoped/Transient, иначе возникает «captive dependency»; если нужен доступ — создавать новый IServiceScope на время операции и извлекать зависимости из него, затем освобождать скоуп. В фоновых задачах и любых контекстах вне HTTP-запроса скоупов по умолчанию нет — их необходимо создавать вручную с помощью IServiceScopeFactory.CreateScope(). В middleware scoped-зависимости получать в InvokeAsync-подписи, а не через конструктор.[^2_5][^2_3][^2_9][^2_10][^2_1][^2_2]

<div style="text-align: center">⁂</div>

[^2_1]: https://www.milanjovanovic.tech/blog/using-scoped-services-from-singletons-in-aspnetcore

[^2_2]: https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines

[^2_3]: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-9.0

[^2_4]: https://stackoverflow.com/questions/73615495/cannot-consume-a-scoped-service-from-singleton

[^2_5]: https://codingblast.com/asp-net-core-dependency-injection-cannot-consume-scoped-service/

[^2_6]: https://github.com/dotnet/sdk/issues/30396

[^2_7]: https://thecodebuzz.com/cannot-consume-scoped-service-from-singleton-ihostedservice/

[^2_8]: https://stackoverflow.com/questions/55708488/using-a-scoped-service-in-a-singleton-in-an-asp-net-core-app

[^2_9]: https://www.roundthecode.com/dotnet-tutorials/how-do-you-resolve-scoped-services-background-service

[^2_10]: https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service

[^2_11]: https://learn.microsoft.com/en-us/answers/questions/1530333/cannot-consume-scoped-from-singlleton-service

[^2_12]: https://www.c-sharpcorner.com/article/how-to-use-scoped-service-from-singelton-service-in-net-core/

[^2_13]: https://our.umbraco.com/forum/using-umbraco-and-getting-started/109237-trouble-with-dependency-injection

[^2_14]: https://codewithmukesh.com/blog/when-to-use-transient-scoped-singleton-dotnet/

[^2_15]: https://www.youtube.com/watch?v=FSjCGdkbiCA

[^2_16]: https://www.reddit.com/r/dotnet/comments/1epf2ro/i_dont_understand_why_shouldnt_every_service_be_a/

[^2_17]: https://github.com/AutoMapper/AutoMapper/issues/2569

[^2_18]: https://github.com/dotnet/docs/issues/29147

[^2_19]: https://www.reddit.com/r/csharp/comments/1de14nx/the_advice_from_the_aspnet_core_in_action_book_is/

