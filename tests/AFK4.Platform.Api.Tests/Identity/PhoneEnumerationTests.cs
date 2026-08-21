using System.Net.Http.Json;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AFK4.Platform.Api.Tests.Identity;

/// <summary>
/// Перебор номеров с одного телефона. Публичная регистрация — единственный маршрут, куда можно
/// принести чужой номер, ничего не зная о нём, поэтому проверка здесь не «работает ли», а
/// «выдаёт ли»: если знакомый номер отвечает хоть чем-то иначе, чем незнакомый, приложение
/// становится справочником «кто играет в этой сети».
/// </summary>
public sealed class PhoneEnumerationTests
{
    private sealed class SilentSmsTransport : ISmsTransport
    {
        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed record Answer(int StatusCode, string Body);

    [Fact]
    public async Task FiftyNumbersInARow_NeverBetrayWhichOnesAreKnown()
    {
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(new SilentSmsTransport());
        });

        // Пары «знакомый — незнакомый» идут подряд, поэтому обе половины пары стоят в очереди
        // рядом: где бы ни сработал лимит по IP, он приходится между парами, а не внутри одной.
        const int pairs = 25;
        var known = new List<string>(pairs);
        for (var index = 0; index < pairs; index++)
        {
            var phone = $"+9929000020{index:D2}";
            await PlatformPersonTestData.AddPersonAsync(factory, phone);
            known.Add(phone);
        }

        using var client = factory.CreateClient();

        for (var index = 0; index < pairs; index++)
        {
            var knownAnswer = await AskAsync(client, known[index]);
            var unknownAnswer = await AskAsync(client, $"+9929000030{index:D2}");

            Assert.Equal(knownAnswer, unknownAnswer);
        }
    }

    private static async Task<Answer> AskAsync(HttpClient client, string phone)
    {
        var response = await client.PostAsJsonAsync(
            "/api/public/register/start", new RegistrationStartRequest(phone));
        return new Answer((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
