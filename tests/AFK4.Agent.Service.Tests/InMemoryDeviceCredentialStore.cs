using AFK4.Agent.Service;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Ключ машины в памяти: тестам не нужен файл, нужен ответ на «каким ключом агент подписался
/// сейчас» и «записал ли он новый».
/// </summary>
internal sealed class InMemoryDeviceCredentialStore(string current = "device-secret") : IDeviceCredentialStore
{
    public string Current { get; private set; } = current;

    public List<string> Updates { get; } = [];

    public void Update(string credentialSecret)
    {
        Updates.Add(credentialSecret);
        Current = credentialSecret;
    }
}
