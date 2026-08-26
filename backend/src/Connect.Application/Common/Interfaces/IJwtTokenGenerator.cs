using Connect.Domain.Entities;

namespace Connect.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
