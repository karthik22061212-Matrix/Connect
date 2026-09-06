using Connect.Application.Common.Models;

namespace Connect.Application.Common.Interfaces;

public interface ITurnCredentialService
{
    TurnCredentialsDto GenerateCredentials(string userId);
}
