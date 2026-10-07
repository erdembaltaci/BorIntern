using Backend.Entities;
using Backend.Repositories;
using Moq;

namespace Backend.Tests;

// Servisler artık ad göstermek için IUserRepository'yi de kullanıyor. Bu testlerin konusu adlar değil;
// bu yüzden "kimse yok" diyen, ama (gerçek depo gibi) null yerine boş liste dönen ortak bir sahte veriyoruz.
public static class TestMocks
{
    public static IUserRepository EmptyUsers()
    {
        var mock = new Mock<IUserRepository>();
        mock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new List<User>());
        return mock.Object;
    }
}
