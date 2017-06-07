var path = require('path');
var express = require('express');
var http = require('http');
var ws = new require('ws');

var app = express();
app.use("/", express.static(path.resolve(__dirname + '/web/')));

var server = http.Server(app);
const wss = new ws.Server({ server : server });

server.listen(3001, function()
{
    console.log('ws listening on *:3001');
});

const interval = setInterval(function ping() 
{
    wss.clients.forEach(function each(ws) 
    {
        if (ws.isAlive === false) return ws.terminate();
        console.log("ping");
        ws.isAlive = false;
        ws.ping('', false, true);
    });
}, 
30000);

function heartbeat() 
{
    this.isAlive = true;
}

wss.on('connection', function connection(ws) 
{
    ws.isAlive = true;
    ws.on('pong', heartbeat);
    console.log("new connection");
    ws.on('message', function incoming(msg) 
    {
        console.log('received: %s', msg);
    });
    ws.send('ping');
});

