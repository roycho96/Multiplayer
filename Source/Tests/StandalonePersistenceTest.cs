using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Multiplayer.Common;

namespace Tests;

[TestFixture]
public class StandalonePersistenceTest
{
    private string tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), $"mp-standalone-persistence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        MultiplayerServer.instance = null;

        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
    }

    [Test]
    public void WriteJoinPoint_PersistsCommandsAndTickForReload()
    {
        var server = MultiplayerServer.instance = new MultiplayerServer(new ServerSettings())
        {
            IsStandaloneServer = true,
            persistence = new StandalonePersistence(tempDir),
        };

        server.worldData.savedGame = [1, 2, 3];
        server.worldData.sessionData = [4, 5, 6];
        server.worldData.mapData[7] = [7, 8, 9];

        var worldCmd = ScheduledCommand.Serialize(new ScheduledCommand(CommandType.Sync, 1234, 1, ScheduledCommand.Global, 5, [10]));
        var mapCmd = ScheduledCommand.Serialize(new ScheduledCommand(CommandType.Designator, 1234, 1, 7, 5, [11]));
        server.worldData.mapCmds[ScheduledCommand.Global] = [worldCmd];
        server.worldData.mapCmds[7] = [mapCmd];

        server.persistence.WriteJoinPoint(server.worldData, 1234);

        var reloadedServer = MultiplayerServer.instance = new MultiplayerServer(new ServerSettings())
        {
            IsStandaloneServer = true,
            persistence = new StandalonePersistence(tempDir),
        };

        var info = reloadedServer.persistence.LoadInto(reloadedServer);

        Assert.That(info, Is.Null);
        Assert.That(reloadedServer.gameTimer, Is.EqualTo(1234));
        Assert.That(reloadedServer.startingTimer, Is.EqualTo(1234));
        Assert.That(reloadedServer.worldData.savedGame, Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.That(reloadedServer.worldData.sessionData, Is.EqualTo(new byte[] { 4, 5, 6 }));
        Assert.That(reloadedServer.worldData.mapData[7], Is.EqualTo(new byte[] { 7, 8, 9 }));
        Assert.That(reloadedServer.worldData.mapCmds[ScheduledCommand.Global], Has.Count.EqualTo(1));
        Assert.That(reloadedServer.worldData.mapCmds[7], Has.Count.EqualTo(1));

        var reloadedWorldCmd = ScheduledCommand.Deserialize(new ByteReader(reloadedServer.worldData.mapCmds[ScheduledCommand.Global][0]));
        var reloadedMapCmd = ScheduledCommand.Deserialize(new ByteReader(reloadedServer.worldData.mapCmds[7][0]));

        Assert.That(reloadedWorldCmd.ticks, Is.EqualTo(1234));
        Assert.That(reloadedMapCmd.ticks, Is.EqualTo(1234));
        Assert.That(reloadedServer.worldData.standaloneWorldSnapshot.tick, Is.EqualTo(1234));
        Assert.That(reloadedServer.worldData.standaloneMapSnapshots[7].tick, Is.EqualTo(1234));
    }

    [Test]
    public void LoadInto_FallsBackToLatestReplaySectionTick()
    {
        var persistence = new StandalonePersistence(tempDir);
        persistence.EnsureDirectories();

        File.WriteAllBytes(Path.Combine(tempDir, "Saved", "world.dat"), [1]);
        File.WriteAllBytes(Path.Combine(tempDir, "Saved", "session.dat"), []);
        File.WriteAllBytes(Path.Combine(tempDir, "Saved", "world_cmds.dat"), ScheduledCommand.SerializeCmds(new List<ScheduledCommand>()));
        File.WriteAllBytes(Path.Combine(tempDir, "Saved", "info.xml"), ReplayInfo.Write(new ReplayInfo
        {
            protocol = MpVersion.Protocol,
            sections = new List<ReplaySection>
            {
                new(100, 100),
                new(999, 999),
            }
        }));

        var server = MultiplayerServer.instance = new MultiplayerServer(new ServerSettings())
        {
            IsStandaloneServer = true,
            persistence = persistence,
        };

        persistence.LoadInto(server);

        Assert.That(server.gameTimer, Is.EqualTo(999));
        Assert.That(server.startingTimer, Is.EqualTo(999));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SeedFromSaveZip_RejectsMissingOrWrongProtocolBeforeWriting(bool includeInfo)
    {
        var persistence = new StandalonePersistence(tempDir);
        persistence.EnsureDirectories();
        var sentinelPath = Path.Combine(persistence.SavedDir, "keep.dat");
        File.WriteAllBytes(sentinelPath, [9, 8, 7]);
        var before = Directory.GetFiles(persistence.SavedDir, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(persistence.SavedDir, path), File.ReadAllBytes);

        var zipPath = Path.Combine(tempDir, "seed.zip");
        CreateReplayZip(zipPath, includeInfo ? new ReplayInfo { protocol = 56009601 } : null);

        Assert.Throws<InvalidDataException>(() => persistence.SeedFromSaveZip(zipPath));

        var after = Directory.GetFiles(persistence.SavedDir, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(persistence.SavedDir, path), File.ReadAllBytes);
        Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
        foreach (var (name, contents) in before)
            Assert.That(after[name], Is.EqualTo(contents));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LoadInto_RejectsUnknownOrWrongProtocolBeforeWorldMutation(bool writeWrongMarker)
    {
        var persistence = new StandalonePersistence(tempDir);
        persistence.EnsureDirectories();
        File.WriteAllBytes(Path.Combine(persistence.SavedDir, "world.dat"), [1, 2, 3]);
        if (writeWrongMarker)
            File.WriteAllBytes(Path.Combine(persistence.SavedDir, "protocol.bin"), BitConverter.GetBytes(56009601));

        var server = MultiplayerServer.instance = new MultiplayerServer(new ServerSettings());
        server.worldData.savedGame = [9];
        server.worldData.sessionData = [8];
        server.worldData.mapData[7] = [7];
        server.gameTimer = 111;
        server.startingTimer = 222;

        Assert.Throws<InvalidDataException>(() => persistence.LoadInto(server));
        Assert.That(server.worldData.savedGame, Is.EqualTo(new byte[] { 9 }));
        Assert.That(server.worldData.sessionData, Is.EqualTo(new byte[] { 8 }));
        Assert.That(server.worldData.mapData, Has.Count.EqualTo(1));
        Assert.That(server.worldData.mapData[7], Is.EqualTo(new byte[] { 7 }));
        Assert.That(server.gameTimer, Is.EqualTo(111));
        Assert.That(server.startingTimer, Is.EqualTo(222));
    }

    [Test]
    public void SeedFromSaveZip_CurrentProtocolCanLoad()
    {
        var zipPath = Path.Combine(tempDir, "seed.zip");
        CreateReplayZip(zipPath, new ReplayInfo
        {
            protocol = MpVersion.Protocol,
            sections = [new ReplaySection(42, 42)],
        });

        var persistence = new StandalonePersistence(tempDir);
        persistence.SeedFromSaveZip(zipPath);
        var server = MultiplayerServer.instance = new MultiplayerServer(new ServerSettings());
        var info = persistence.LoadInto(server);

        Assert.That(info?.protocol, Is.EqualTo(MpVersion.Protocol));
        Assert.That(server.worldData.savedGame, Is.Not.Empty);
        Assert.That(server.gameTimer, Is.EqualTo(42));
        Assert.That(BitConverter.ToInt32(File.ReadAllBytes(Path.Combine(persistence.SavedDir, "protocol.bin"))),
            Is.EqualTo(MpVersion.Protocol));
    }

    [Test]
    public void WriteWorldSnapshot_StampsProtocolAndCanLoad()
    {
        var persistence = new StandalonePersistence(tempDir);
        persistence.WriteWorldSnapshot([1, 2, 3], [4, 5], 77);
        var server = MultiplayerServer.instance = new MultiplayerServer(new ServerSettings());

        Assert.DoesNotThrow(() => persistence.LoadInto(server));
        Assert.That(server.worldData.savedGame, Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.That(server.worldData.sessionData, Is.EqualTo(new byte[] { 4, 5 }));
        Assert.That(server.gameTimer, Is.EqualTo(77));
        Assert.That(BitConverter.ToInt32(File.ReadAllBytes(Path.Combine(persistence.SavedDir, "protocol.bin"))),
            Is.EqualTo(MpVersion.Protocol));
    }

    private static void CreateReplayZip(string path, ReplayInfo? info)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        if (info != null)
        {
            var infoEntry = zip.CreateEntry("info");
            using var infoStream = infoEntry.Open();
            infoStream.Write(ReplayInfo.Write(info));
        }

        var worldEntry = zip.CreateEntry("world/000_save");
        using var worldStream = worldEntry.Open();
        worldStream.Write([1, 2, 3]);
    }
}
