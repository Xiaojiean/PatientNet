#!/usr/bin/env nodejs

//Followed example in https://github.com/dimircea/WebRTC/blob/master/SimpleVideoChat/server.js

const WebSocketServer = require('ws').Server,
	express	 = require('express'),
	bodyParser = require('body-parser'),
	http = require('https'),
	app = express(),
	fs = require('fs');

var cfg = {
	ssl: true,
	ssl_key: './ssl/privkey.pem',
	ssl_cert:'./ssl/fullchain.pem'
};
var wsClients = [];
//Keys for TWilio
var sid = 'ACe01de1c8c080e8ef0548b528f7e4460f';
var token = 'b193e08a8168d978dbef62d0fe4fe035';
var twilioClient = require('twilio')(sid, token);

const pKey = fs.readFileSync(cfg.ssl_key),
	pCert = fs.readFileSync(cfg.ssl_cert),
	options = {key: pKey, cert: pCert};

app.use(bodyParser.urlencoded({extended: false}));
app.use(bodyParser.json());

app.use(function(req, res, next) {
	if(req.headers['x-forwarded-proto']==='http') {
		return res.redirect(['https://', req.get('host'), req.url].join(''));
	}
	next();
});

app.post('/api/v1/sendsms', function(req, res) {
	console.log('Received: ' + req.body.number);
	var obj = {};
	obj['number'] = req.body.number;
	obj['message'] = JSON.stringify(req.body);
	if(wsClients.length == 0) {
		res.status(500).send("No doctors connected");
		return;
	}
	for(var i = wsClients.length - 1; i >= 0; i--) {
		var cli = wsClients[i];
		if(cli.readyState === cli.OPEN){
			cli.send(JSON.stringify(obj));
		} else {
			cli.close();
			wsClients.splice(i, 1);
		}
	}
	res.status(200).send("OK");
});


var server = http.createServer({key:pKey, cert:pCert}, app).listen(3001, function(){
	console.log("server running at https://localhost:3001");
});
var wss = new WebSocketServer({server: server});

/* on connect */
wss.on('connection', function(client) {
	console.log("A new WebSocket client was connected.");
	wsClients.push(client);
	client.on('message', function(message) {
		var msg = JSON.parse(message);
		if(msg.link && msg.number){
			twilioClient.messages.create({
				to: "+1" + msg.number,
				from: "+12062026089",
				body: "View the patient at " + msg.link,
			}, function(err, message) {
				if (err){
					console.log(err);
				}
		
			});
		}
	});
});
