const { WrappedWebSocketServer, WrappedTcpServer } = require("./connections")
const { RoomServer } = require("./rooms");
const { IceServerProvider } = require("./ice");
const { Performance } = require('./logging')
const nconf = require('nconf');

// nconf loads the configuration hierarchically; default.json contains most of
// the rarely changing configuration properties, stored with the branch.
// Additional configuration files - where present - add or override parameters,
// such as pre-shared secrets, that should not be in source control.
nconf.file('local', 'config/local.json');
nconf.file('default', 'config/default.json');

roomServer = new RoomServer();
roomServer.addServer(new WrappedTcpServer(nconf.get('roomserver:ports:tcp')));
roomServer.addServer(new WrappedWebSocketServer(nconf.get('roomserver:ports:ws')));

iceServerProvider = new IceServerProvider(roomServer);
var iceServers = nconf.get('iceservers');
if (iceServers){
    for (const iceServer of iceServers){
        iceServerProvider.addIceServer(
            iceServer.uri,
            iceServer.secret,
            iceServer.timeoutSeconds,
            iceServer.refreshSeconds,
            iceServer.username,
            iceServer.password);
    }
}

Performance.startLog("C:/Users/Sebastian/AppData/LocalLow/UCL/ubiq");
dateNow = new Date();
secondsNow = dateNow.getSeconds() + dateNow.getHours() * 60;
Performance.log("Sync", secondsNow);