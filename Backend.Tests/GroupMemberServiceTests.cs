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

    [Fact]
    public async Task AddMemberAsync_ZatenUyeyse_InvalidOperationExceptionFirlatir()
    {
        var group = new Group { Id = 1, Name = "Test Grubu", MentorId = 1 };

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var mockGroupMemberRepository = new Mock<IGroupMemberRepository>();
        mockGroupMemberRepository.Setup(r => r.IsUserInGroupAsync(1, 5)).ReturnsAsync(true);

        var service = new GroupMemberService(mockGroupRepository.Object, mockGroupMemberRepository.Object);

        var request = new AddGroupMemberRequestDto { UserId = 5 };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddMemberAsync(mentorId: 1, groupId: 1, request));
    }

    [Fact]
    public async Task GetGroupMembersAsync_NeMentorNeUyeyse_ForbiddenExceptionFirlatir()
    {
        var group = new Group { Id = 1, Name = "Test Grubu", MentorId = 99 };

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var mockGroupMemberRepository = new Mock<IGroupMemberRepository>();
        mockGroupMemberRepository.Setup(r => r.IsUserInGroupAsync(1, 7)).ReturnsAsync(false);

        var service = new GroupMemberService(mockGroupRepository.Object, mockGroupMemberRepository.Object);

        // callerId=7, ne grubun mentoru (99) ne de üyesi -> Forbidden beklenir.
        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.GetGroupMembersAsync(callerId: 7, groupId: 1));
    }

    [Fact]
    public async Task GetGroupMembersAsync_GrubunUyesiyse_ListeyiGorebilir()
    {
        var group = new Group { Id = 1, Name = "Test Grubu", MentorId = 99 };

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var mockGroupMemberRepository = new Mock<IGroupMemberRepository>();
        // callerId=7, mentor değil ama grubun bir üyesi -> yine de görebilmeli.
        mockGroupMemberRepository.Setup(r => r.IsUserInGroupAsync(1, 7)).ReturnsAsync(true);
        mockGroupMemberRepository.Setup(r => r.GetByGroupIdAsync(1)).ReturnsAsync(new List<GroupMember>
        {
            new() { Id = 1, GroupId = 1, UserId = 7 }
        });

        var service = new GroupMemberService(mockGroupRepository.Object, mockGroupMemberRepository.Object);

        var result = await service.GetGroupMembersAsync(callerId: 7, groupId: 1);

        Assert.Single(result);
    }

    [Fact]
    public async Task RemoveMemberAsync_GrupBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Group?)null);

        var service = new GroupMemberService(
            mockGroupRepository.Object,
            new Mock<IGroupMemberRepository>().Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.RemoveMemberAsync(mentorId: 1, groupId: 999, userId: 5));
    }

    [Fact]
    public async Task RemoveMemberAsync_BaskaMentorunGrubu_ForbiddenExceptionFirlatir()
    {
        var group = new Group { Id = 1, Name = "Test Grubu", MentorId = 99 };

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var service = new GroupMemberService(
            mockGroupRepository.Object,
            new Mock<IGroupMemberRepository>().Object);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.RemoveMemberAsync(mentorId: 1, groupId: 1, userId: 5));
    }

    [Fact]
    public async Task RemoveMemberAsync_UyelikBulunamazsa_NotFoundExceptionFirlatir()
    {
        var group = new Group { Id = 1, Name = "Test Grubu", MentorId = 1 };

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var mockGroupMemberRepository = new Mock<IGroupMemberRepository>();
        mockGroupMemberRepository.Setup(r => r.GetByGroupAndUserAsync(1, 5)).ReturnsAsync((GroupMember?)null);

        var service = new GroupMemberService(mockGroupRepository.Object, mockGroupMemberRepository.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.RemoveMemberAsync(mentorId: 1, groupId: 1, userId: 5));
    }

    [Fact]
    public async Task RemoveMemberAsync_GecerliIstek_SoftDeleteYapilir()
    {
        var group = new Group { Id = 1, Name = "Test Grubu", MentorId = 1 };
        var membership = new GroupMember { Id = 1, GroupId = 1, UserId = 5, IsDeleted = false };

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var mockGroupMemberRepository = new Mock<IGroupMemberRepository>();
        mockGroupMemberRepository.Setup(r => r.GetByGroupAndUserAsync(1, 5)).ReturnsAsync(membership);

        var service = new GroupMemberService(mockGroupRepository.Object, mockGroupMemberRepository.Object);

        await service.RemoveMemberAsync(mentorId: 1, groupId: 1, userId: 5);

        Assert.True(membership.IsDeleted);
        Assert.NotNull(membership.DeletedAt);
    }
}
