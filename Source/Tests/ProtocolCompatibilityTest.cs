using System.Reflection;
using Multiplayer.Common;
using Multiplayer.Common.Networking.Packet;

namespace Tests;

[NonParallelizable]
public class ProtocolCompatibilityTest
{
    private MultiplayerServer server = null!;
    private RecordingConnection connection = null!;
    private ServerJoiningState joiningState = null!;

    [SetUp]
    public void SetUp()
    {
        server = MultiplayerServer.instance = new MultiplayerServer(new ServerSettings());
        connection = new RecordingConnection("protocol-test");

        MpConnectionState.SetImplementation(ConnectionStateEnum.ServerJoining, typeof(NoopServerState));
        connection.ChangeState(ConnectionStateEnum.ServerJoining);

        var player = new ServerPlayer(1, connection);
        connection.serverPlayer = player;
        server.playerManager.Players.Add(player);
        joiningState = new ServerJoiningState(connection);
    }

    [TearDown]
    public void TearDown()
    {
        MpConnectionState.SetImplementation(ConnectionStateEnum.ServerJoining, typeof(ServerJoiningState));
    }

    [Test]
    public void CurrentForkProtocolIsAccepted()
    {
        var accepted = HandleProtocol(ClientProtocolPacket.Current());

        Assert.That(accepted, Is.True);
        Assert.That(connection.LastGoodbye, Is.Null);
        Assert.That(connection.SentPackets, Does.Contain(Packets.Server_ProtocolOk));
    }

    [TestCase(56)]
    [TestCase(57)]
    [TestCase(56009601)]
    public void IncompatibleProtocolIsRejectedWithForkIdentity(int protocol)
    {
        var accepted = HandleProtocol(new ClientProtocolPacket(protocol));

        Assert.That(accepted, Is.False);
        Assert.That(connection.LastGoodbye, Is.Not.Null);
        Assert.That(connection.LastGoodbye!.Value.reason, Is.EqualTo(MpDisconnectReason.Protocol));

        var reader = new ByteReader(connection.LastGoodbye.Value.data);
        Assert.That(reader.ReadString(), Is.EqualTo(MpVersion.Version));
        Assert.That(reader.ReadInt32(), Is.EqualTo(MpVersion.Protocol));
        Assert.That(reader.Left, Is.Zero);
    }

    [Test]
    public void ForkProtocolAndDisplayVersionRemainDistinct()
    {
        Assert.That(MpVersion.Protocol, Is.EqualTo(57009602));
        Assert.That(MpVersion.SimpleVersion, Is.EqualTo("0.11.5"));
        Assert.That(MpVersion.ForkVersion, Is.EqualTo("0.11.5-roycho96.2"));
        Assert.That(MpVersion.Version, Does.StartWith(MpVersion.ForkVersion));
    }

    private bool HandleProtocol(ClientProtocolPacket packet)
    {
        var method = typeof(ServerJoiningState).GetMethod("HandleProtocol", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (bool)method!.Invoke(joiningState, [packet])!;
    }

    private sealed class NoopServerState(ConnectionBase connection) : MpConnectionState(connection);
}
