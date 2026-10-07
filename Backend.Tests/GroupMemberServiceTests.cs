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
            new Mock<IGroupMemberRepository>().Object, new Mock<IUserRepository>().Object);

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
            new Mock<IGroupMemberRepository>().Object, new Mock<IUserRepository>().Object);

        var request = new AddGroupMemberRequestDto { UserId = 5 };

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.AddMemberAsync(mentorId: 1, groupId: 999, request));
    }

    [Fact]
    public async Task AddMemberAsync_ZatenUyeyse_ConflictExceptionFirlatir()
    {
        var group = new Group { Id = 1, Name = "Test Grubu", MentorId = 1 };

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var mockGroupMemberRepository = new Mock<IGroupMemberRepository>();
        mockGroupMemberRepository.Setup(r => r.IsUserInGroupAsync(1, 5)).ReturnsAsync(true);

        var service = new GroupMemberService(mockGroupRepository.Object, mockGroupMemberRepository.Object, new Mock<IUserRepository>().Object);

        var request = new AddGroupMemberRequestDto { UserId = 5 };

        await Assert.ThrowsAsync<ConflictException>(
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

        var service = new GroupMemberService(mockGroupRepository.Object, mockGroupMemberRepository.Object, new Mock<IUserRepository>().Object);

        // callerId=7, ne grubun mentoru (99) ne de üyesi -> Forbidden beklenir.
        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.GetGroupMembersAsync(callerId: 7, groupId: 1, page: 1, pageSize: 20));
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
        mockGroupMemberRepository.Setup(r => r.GetPagedByGroupIdAsync(1, 1, 20)).ReturnsAsync((new List<GroupMember>
        {
            new() { Id = 1, GroupId = 1, UserId = 7 }
        }, 1));

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new List<User>());

        var service = new GroupMemberService(mockGroupRepository.Object, mockGroupMemberRepository.Object, mockUserRepository.Object);

        var result = await service.GetGroupMembersAsync(callerId: 7, groupId: 1, page: 1, pageSize: 20);

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task RemoveMemberAsync_GrupBulunamazsa_NotFoundExceptionFirlatir()
    {
        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Group?)null);

        var service = new GroupMemberService(
            mockGroupRepository.Object,
            new Mock<IGroupMemberRepository>().Object, new Mock<IUserRepository>().Object);

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
            new Mock<IGroupMemberRepository>().Object, new Mock<IUserRepository>().Object);

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

        var service = new GroupMemberService(mockGroupRepository.Object, mockGroupMemberRepository.Object, new Mock<IUserRepository>().Object);

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

        var service = new GroupMemberService(mockGroupRepository.Object, mockGroupMemberRepository.Object, new Mock<IUserRepository>().Object);

        await service.RemoveMemberAsync(mentorId: 1, groupId: 1, userId: 5);

        Assert.True(membership.IsDeleted);
        Assert.NotNull(membership.DeletedAt);
    }

    [Fact]
    public async Task AddMemberAsync_KullaniciYoksa_NotFoundExceptionFirlatir()
    {
        var group = new Group { Id = 1, Name = "Test Grubu", MentorId = 1 };

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync((User?)null);

        var service = new GroupMemberService(
            mockGroupRepository.Object, new Mock<IGroupMemberRepository>().Object, mockUserRepository.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.AddMemberAsync(mentorId: 1, groupId: 1, new AddGroupMemberRequestDto { UserId = 5 }));
    }

    [Theory]
    [InlineData(UserRole.Mentor, UserStatus.Active)]
    [InlineData(UserRole.Admin, UserStatus.Active)]
    [InlineData(UserRole.Intern, UserStatus.Pending)]
    [InlineData(UserRole.Intern, UserStatus.Inactive)]
    public async Task AddMemberAsync_AktifStajyerDegilse_InvalidOperationExceptionFirlatir(UserRole role, UserStatus status)
    {
        var group = new Group { Id = 1, Name = "Test Grubu", MentorId = 1 };

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(5))
            .ReturnsAsync(new User { Id = 5, FullName = "Ali", Email = "ali@mail.com", Role = role, Status = status });

        var service = new GroupMemberService(
            mockGroupRepository.Object, new Mock<IGroupMemberRepository>().Object, mockUserRepository.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddMemberAsync(mentorId: 1, groupId: 1, new AddGroupMemberRequestDto { UserId = 5 }));
    }

    [Fact]
    public async Task AddMemberAsync_AktifStajyer_UyeEklenirVeAdiDonerdeGelir()
    {
        var group = new Group { Id = 1, Name = "Test Grubu", MentorId = 1 };

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(
            new User { Id = 5, FullName = "Ayşe Yılmaz", Email = "ayse@mail.com", Role = UserRole.Intern, Status = UserStatus.Active });

        var mockGroupMemberRepository = new Mock<IGroupMemberRepository>();

        var service = new GroupMemberService(
            mockGroupRepository.Object, mockGroupMemberRepository.Object, mockUserRepository.Object);

        var result = await service.AddMemberAsync(mentorId: 1, groupId: 1, new AddGroupMemberRequestDto { UserId = 5 });

        Assert.Equal(5, result.UserId);
        Assert.Equal("Ayşe Yılmaz", result.FullName);
        Assert.Equal("ayse@mail.com", result.Email);
        mockGroupMemberRepository.Verify(r => r.AddAsync(It.IsAny<GroupMember>()), Times.Once);
        mockGroupMemberRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetGroupMembersAsync_UyelerinAdiVeEpostasiDoldurulur()
    {
        var group = new Group { Id = 1, Name = "Test Grubu", MentorId = 99 };

        var mockGroupRepository = new Mock<IGroupRepository>();
        mockGroupRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(group);

        var mockGroupMemberRepository = new Mock<IGroupMemberRepository>();
        mockGroupMemberRepository.Setup(r => r.GetPagedByGroupIdAsync(1, 1, 20)).ReturnsAsync((new List<GroupMember>
        {
            new() { Id = 1, GroupId = 1, UserId = 7 },
            new() { Id = 2, GroupId = 1, UserId = 8 }
        }, 2));

        // 8 numaralı kullanıcı bulunamıyor (ör. silinmiş kayıt): liste yine de dönmeli, ad boş kalmalı.
        var mockUserRepository = new Mock<IUserRepository>();
        mockUserRepository.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new List<User>
        {
            new() { Id = 7, FullName = "Mehmet Demir", Email = "mehmet@mail.com" }
        });

        var service = new GroupMemberService(
            mockGroupRepository.Object, mockGroupMemberRepository.Object, mockUserRepository.Object);

        // callerId=99: grubun mentoru, listeyi görebilir.
        var result = await service.GetGroupMembersAsync(callerId: 99, groupId: 1, page: 1, pageSize: 20);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Mehmet Demir", result.Items[0].FullName);
        Assert.Equal("mehmet@mail.com", result.Items[0].Email);
        Assert.Equal(string.Empty, result.Items[1].FullName);
    }
}
