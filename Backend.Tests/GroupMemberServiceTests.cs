using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;
using Backend.Services;
using Moq;
using Xunit;

namespace Backend.Tests;

// Bu dosya, "sahiplik kontrolü" (ownership check) mantığının test edilmesine örnek.
public class GroupMemberServiceTests
{
    [Fact]
    public async Task AddMemberAsync_BaskaMentorunGrubunaEklemeyeCalisirsa_ForbiddenExceptionFirlatir()
    {
        // Arrange: Grup, 99 numaralı mentor'a ait. Id, BaseEntity'den miras alınan
        // (public get/set) bir alan olduğu için object initializer içinde de verilebilir.
        var group = new Group { Id = 1, Name = "Test Grubu", MentorId = 99 };

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var service = new GroupMemberService(
            mockGroupRepository.Object,
            new Mock<IGroupMemberRepository>().Object);

        var request = new AddGroupMemberRequestDto { UserId = 5 };

        // Act + Assert: mentorId=1 gönderiyoruz ama grup 99 numaralı mentor'a ait -> Forbidden beklenir.
        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.AddMemberAsync(mentorId: 1, groupId: 1, request));
    }

    [Fact]
    public async Task AddMemberAsync_GrupBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Group?)null);

        var service = new GroupMemberService(
            mockGroupRepository.Object,
            new Mock<IGroupMemberRepository>().Object);

        var request = new AddGroupMemberRequestDto { UserId = 5 };

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.AddMemberAsync(mentorId: 1, groupId: 999, request));
    }
}
