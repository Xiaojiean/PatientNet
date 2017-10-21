#!/usr/bin/env nodejs

//Followed example in https://github.com/dimircea/WebRTC/blob/master/SimpleVideoChat/server.js

const WebSocketServer = require('ws').Server,
	express	 = require('express'),
	bodyParser = require('body-parser'),
	http = require('https'),
	app = express(),
	fs = require('fs'),
	config = require('./config.js');

var cfg = {
	ssl: true,
	ssl_key: './ssl/privkey.pem',
	ssl_cert:'./ssl/fullchain.pem'
};
var wsClients = [];
//Keys for TWilio
var twilioClient = require('twilio')(config.twilioSid, config.twilioKey);

//Keys for SendGrid
const sgMail = require('@sendgrid/mail');
sgMail.setApiKey(config.sendGridKey);
const emailBody = "You have been called as an Emergency Contact. Start a conversation with the doctor here: ";
const emailMsg = {
	from: 'no-reply@patientnet2.com',
	subject: 'You have been selected as an Emergency Contact!',
};

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
	console.log('/api/v1/sendsms received: ' + JSON.stringify(req.body));
	var obj = {};
	obj['type'] = 'sms';
	obj['number'] = req.body.number;
	obj['message'] = JSON.stringify(req.body);
	sendMessage(obj);
	res.status(200).send("OK");
});

app.post('/api/v1/sendemail', function(req, res) {
	console.log('/api/v1/sendemail received: ' + JSON.stringify(req.body));
	var obj = {};
	obj['type'] = 'email';
	obj['email'] = req.body.email;
	obj['message'] = JSON.stringify(req.body);
	sendMessage(obj);
	res.status(200).send("OK");
});

app.post('/api/v1/requestdoctor', function(req, res) {
	console.log('/api/v1/requestdoctor received: ' + JSON.stringify(req.body));
	var obj = {};
	obj['type'] = 'request';
	obj['skypeid'] = req.body.skypeid;
	obj['message'] = JSON.stringify(req.body);
	sendMessage(obj);
	res.status(200).send("OK");
});

function sendMessage(obj){
	if(wsClients.length == 0) {
		//res.status(500).send("No doctors connected");
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
}

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
		if(msg.type == 'sms'){
			twilioClient.messages.create({
				to: "+1" + msg.number,
				from: "+12062026089",
				body: "You have been called as an Emergency Contact. Start a conversation with the doctor here: " + msg.link,
			}, function(err, message) {
				if (err){
					console.log(err);
				}
		
			});
		} else if (msg.type == 'email') {
			emailMsg.to = msg.email;
			emailMsg.text = emailBody + msg.link;
			sgMail.send(emailMsg);
		}
	});
});
