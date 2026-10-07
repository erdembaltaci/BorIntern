using Backend.Dtos;
using Backend.Entities;
using Backend.Exceptions;
using Backend.Repositories;
using Backend.Services;
using Moq;
using Xunit;

namespace Backend.Tests;

public class AnnouncementServiceTests
{
    // Her testte aynı dört bağımlılığı kurmamak için küçük bir yardımcı.
    private static (AnnouncementService Service, Mock<IAnnouncementRepository> Announcements, Mock<IGroupRepository> Groups,
        Mock<IGroupMemberRepository> Members, Mock<IUserRepository> Users) Build()
    {
        var announcements = new Mock<IAnnouncementRepository>();
        var groups = new Mock<IGroupRepository>();
        var members = new Mock<IGroupMemberRepository>();
        var users = new Mock<IUserRepository>();
        var service = new AnnouncementService(announcements.Object, groups.Object, members.Object, users.Object);
        return (service, announcements, groups, members, users);
    }

    private static CreateAnnouncementRequestDto Request() =>
        new() { Title = "  Toplantı  ", Content = "  Yarın saat 10:00  " };

    [Fact]
    public async Task CreateAsync_GrupYoksa_NotFoundExceptionFirlatir()
    {
        var t = Build();
        t.Groups.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Group?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => t.Service.CreateAsync(mentorId: 1, groupId: 1, Request()));
    }

    [Fact]
    public async Task CreateAsync_BaskaMentorunGrubu_ForbiddenExceptionFirlatir()
    {
        var t = Build();
        t.Groups.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Group { Id = 1, Name = "G", MentorId = 99 });

        await Assert.ThrowsAsync<ForbiddenException>(() => t.Service.CreateAsync(mentorId: 1, groupId: 1, Request()));
        t.Announcements.Verify(r => r.AddAsync(It.IsAny<Announcement>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_GrupSahibi_KaydederVeMetinleriKirpar()
    {
        var t = Build();
        t.Groups.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Group { Id = 1, Name = "Yaz Stajı", MentorId = 5 });
        t.Users.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(new User { Id = 5, FullName = "Zeynep Kaya" });

        Announcement? saved = null;
        t.Announcements.Setup(r => r.AddAsync(It.IsAny<Announcement>())).Callback<Announcement>(a => saved = a).Returns(Task.CompletedTask);

        var result = await t.Service.CreateAsync(mentorId: 5, groupId: 1, Request());

        Assert.NotNull(saved);
        Assert.Equal("Toplantı", saved!.Title);
        Assert.Equal("Yarın saat 10:00", saved.Content);
        Assert.Equal(1, saved.GroupId);
        Assert.Equal(5, saved.MentorId);
        Assert.Equal("Yaz Stajı", result.GroupName);
        Assert.Equal("Zeynep Kaya", result.MentorName);
        t.Announcements.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetGroupAnnouncementsAsync_NeMentorNeUyeyse_ForbiddenExceptionFirlatir()
    {
        var t = Build();
        t.Groups.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Group { Id = 1, MentorId = 99 });
        t.Members.Setup(r => r.IsUserInGroupAsync(1, 7)).ReturnsAsync(false);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => t.Service.GetGroupAnnouncementsAsync(callerId: 7, groupId: 1, page: 1, pageSize: 20));
    }

    [Fact]
    public async Task GetGroupAnnouncementsAsync_GrubunUyesiyse_ListeyiAdlariylaGorur()
    {
        var t = Build();
        t.Groups.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Group { Id = 1, Name = "Yaz Stajı", MentorId = 99 });
        t.Members.Setup(r => r.IsUserInGroupAsync(1, 7)).ReturnsAsync(true);
        t.Announcements.Setup(r => r.GetPagedByGroupIdAsync(1, 1, 20)).ReturnsAsync((new List<Announcement>
        {
            new() { Id = 1, GroupId = 1, MentorId = 99, Title = "A", Content = "a" }
        }, 1));
        t.Groups.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new List<Group> { new() { Id = 1, Name = "Yaz Stajı" } });
        t.Users.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new List<User> { new() { Id = 99, FullName = "Zeynep Kaya" } });

        var result = await t.Service.GetGroupAnnouncementsAsync(callerId: 7, groupId: 1, page: 1, pageSize: 20);

        var item = Assert.Single(result.Items);
        Assert.Equal("Yaz Stajı", item.GroupName);
        Assert.Equal("Zeynep Kaya", item.MentorName);
    }

    [Fact]
    public async Task GetMyAnnouncementsAsync_GecersizSayfaDegerleriVarsayilanaCekilir()
    {
        var t = Build();
        t.Announcements.Setup(r => r.GetPagedForMemberAsync(7, 1, 20)).ReturnsAsync((new List<Announcement>(), 0));
        t.Groups.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new List<Group>());
        t.Users.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new List<User>());

        var result = await t.Service.GetMyAnnouncementsAsync(userId: 7, page: 0, pageSize: -5);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        t.Announcements.Verify(r => r.GetPagedForMemberAsync(7, 1, 20), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_BaskaMentorunGrubu_ForbiddenExceptionFirlatir()
    {
        var t = Build();
        t.Groups.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Group { Id = 1, MentorId = 99 });

        await Assert.ThrowsAsync<ForbiddenException>(() => t.Service.DeleteAsync(mentorId: 1, groupId: 1, announcementId: 3));
    }

    [Fact]
    public async Task DeleteAsync_DuyuruBaskaGrubaAitse_NotFoundExceptionFirlatir()
    {
        var t = Build();
        t.Groups.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Group { Id = 1, MentorId = 1 });
        // Duyuru 2 numaralı gruba ait: 1 numaralı grubun adresi üzerinden silinememeli.
        t.Announcements.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(new Announcement { Id = 3, GroupId = 2 });

        await Assert.ThrowsAsync<NotFoundException>(() => t.Service.DeleteAsync(mentorId: 1, groupId: 1, announcementId: 3));
    }

    [Fact]
    public async Task DeleteAsync_GecerliIstek_SoftDeleteYapilir()
    {
        var t = Build();
        var announcement = new Announcement { Id = 3, GroupId = 1, IsDeleted = false };
        t.Groups.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Group { Id = 1, MentorId = 1 });
        t.Announcements.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(announcement);

        await t.Service.DeleteAsync(mentorId: 1, groupId: 1, announcementId: 3);

        Assert.True(announcement.IsDeleted);
        Assert.NotNull(announcement.DeletedAt);
        t.Announcements.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
