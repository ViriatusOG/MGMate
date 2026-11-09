using MGMate.Core.Models;
using MGMate.Data;
using MGMate.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MGMate.Tests;

public class WarrantyRepositoryTests
{
    private static async Task<WarrantyRepository> SetupAsync(CancellationToken ct)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Vehicles.AddRange(
            new Vehicle { Id = 1, Vin = "FAKEVN00000000001" },
            new Vehicle { Id = 2, Vin = "FAKEVN00000000002" });
        await db.SaveChangesAsync(ct);
        return new WarrantyRepository(db);
    }

    [Fact]
    public async Task AnotherCarsCover_CannotBeChangedOrDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = await SetupAsync(ct);
        var cover = await repo.AddAsync(new WarrantyCover { VehicleId = 1, Name = "Paint", Months = 36 }, ct);

        Assert.Null(await repo.UpdateAsync(2, cover.Id, c => c.Name = "Changed", ct));
        Assert.False(await repo.DeleteAsync(2, cover.Id, ct));
        Assert.Equal("Paint", Assert.Single(await repo.ListAsync(1, ct)).Name);
        Assert.Empty(await repo.ListAsync(2, ct));
    }

    [Fact]
    public async Task List_FollowsSortOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = await SetupAsync(ct);
        await repo.AddAsync(new WarrantyCover { VehicleId = 1, Name = "B", Months = 1, SortOrder = 2 }, ct);
        await repo.AddAsync(new WarrantyCover { VehicleId = 1, Name = "A", Months = 1, SortOrder = 1 }, ct);

        Assert.Equal(["A", "B"], (await repo.ListAsync(1, ct)).Select(c => c.Name));
        Assert.Equal(2, await repo.CountAsync(1, ct));
    }
}
