#!/usr/bin/env nodejs

//Followed example in https://github.com/dimircea/WebRTC/blob/master/SimpleVideoChat/server.js

const WebSocketServer = require('ws').Server,
	express	 = require('express'),
	bodyParser = require('body-parser'),
	http = require('https'),
	app = express(),
	fs = require('fs'),
	config = require('./config.js'),
	schedule = require('node-schedule');

var cfg = {
	ssl: true,
	ssl_key: './ssl/privkey.pem',
	ssl_cert:'./ssl/fullchain.pem'
};
var wsClients = {}; //Stores a map of doctor IDs to a map of web session IDs to web socket clients.
var availDocs = 0; //Number of available doctors.
var emts = [];
//Keys for TWilio
var twilioClient = require('twilio')(config.twilioSid, config.twilioKey);
var stats = {};

schedule.scheduleJob('0 0 0 * * *', function(){
	//Everyday at midnight, reset the statistics.
	stats = {};
});

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

app.post('/api/v1/requestdoctor', function(req, res) {
	console.log('/api/v1/requestdoctor received: ' + JSON.stringify(req.body));
	var obj = {};
	obj['type'] = 'request';
	obj['skypeid'] = req.body.skypeid;
	obj['email'] = req.body.email;
	obj['number'] = req.body.number;
	obj['requestTime'] = new Date();
	obj['message'] = JSON.stringify(req.body);
	emts.push(obj);
	sendMessage(obj);
	res.status(200).send("OK");
});

app.post('/api/v1/getavailabledoctors', function(req, res) {
	console.log('/api/v1/getavailabledoctors received: ' + JSON.stringify(req.body));
	updateClients();
	var obj = {};
	obj['availabledoctors'] = availDocs;
	res.status(200).send(JSON.stringify(obj));
});

function emtAccepted(emtId, webId) {
	for(var i = emts.length - 1; i >= 0; i--) {
		if (emts[i].skypeid == emtId) {
			emts.splice(i, 1);
		}
	}
	var obj = {};
	obj['type'] = 'remove';
	obj['skypeid'] = emtId;
	obj['message'] = 'Remove id: ' + emtId;
	sendMessage(obj);
	availDocs--;
}

function sendMessage(obj){
	updateClients();
	if(Object.keys(wsClients).length == 0) {
		//res.status(500).send("No doctors connected");
		return;
	}
	for (var key in wsClients) {
		var cliList = wsClients[key];
		for (var i in cliList) {
			var cli = cliList[i];	
			if(cli.readyState == cli.OPEN) {
				console.log("Sending obj: " + JSON.stringify(obj) + " to key: " + key);
				cli.send(JSON.stringify(obj));
			}
		}
	}
}

function sendMessageToWeb(obj, docId, webId) {
	for (var key in wsClients) {
		if (key == docId){	
			var cliList = wsClients[key];
			for (var i in cliList) {
				if (i == webId) {
					var cli = cliList[i];
					if(cli.readyState == cli.OPEN) {
						console.log("Sending obj: " + JSON.stringify(obj) + " to key: " + key);
						cli.send(JSON.stringify(obj));
					}
				}
			}
		}
	}	
}

//Check if clients are open, if not, remove them from dictionary.
function updateClients() {
	var tmp = {};
	for (var key in wsClients) {
		var cliList = wsClients[key];
		for (var i in cliList) {
			var cli = cliList[i];
			if(cli.readyState != cli.OPEN) {
				console.log("Removing id: " + i);
				delete cliList[cli];
				if (availDocs > 0){
					availDocs--;
				}
			} else {
				if(!(key in tmp)){
					tmp[key] = {};
				}
				tmp[key][i] = cli;
			}
		}
	}
	wsClients = tmp;
}

var server = http.createServer({key:pKey, cert:pCert}, app).listen(3001, function(){
	console.log("server running at https://localhost:3001");
});
var wss = new WebSocketServer({server: server});

/* on connect */
wss.on('connection', function(client) {
	console.log("A new WebSocket client was connected.");
	//wsClients.push(client);
	var obj = {};
	obj['type'] = 'hello';
	obj['message'] = 'hello';
	client.send(JSON.stringify(obj));
	
	client.on('message', function(message) {
		var msg = JSON.parse(message);
		if (msg.type == 'hello') {
			console.log("received doc id: " + msg.docId, " webId: " + msg.webId);
			if(!(msg.docId in wsClients)) {
				//We haven't seen this doctor yet.
				//Create an object that maps the web session IDs to web socket clients.
				wsClients[msg.docId] = {};
			}
			wsClients[msg.docId][msg.webId] = client;
			for (var i = 0; i < emts.length; i++) {
				client.send(JSON.stringify(emts[i]));
			}
			availDocs++;
			if (msg.docId in stats) {
				var obj = {};
				obj['type'] = 'stats';
				obj['stats'] = JSON.stringify(stats[msg.docId]);
				console.log("Sending stats to: " + msg.docId);
				client.send(JSON.stringify(obj));
			} else {
				stats[msg.docId] = {};
			}
		} else if(msg.type == 'sms'){
			//Received request to send SMS.
			emtAccepted(msg.skypeid, msg.webId);
			console.log("Sending text to: " + msg.number);
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
			//Received request to send email.
			emtAccepted(msg.skypeid, msg.webId);
			console.log("Sending email to: " + msg.email);
			emailMsg.to = msg.email;
			emailMsg.text = emailBody + msg.link;
			sgMail.send(emailMsg);
		} else if (msg.type == 'accept') {
			//Resolved Skype ID without an email or sms request.
			emtAccepted(msg.skypeid, msg.webId);
		} else if (msg.type == 'upload') {
			//Received request from contact to upload a photo.
			//Notify the corresponding doctor web session.
			var payload = {};
			payload['type'] = 'upload';
			payload['filename'] = msg.filename;
			payload['handle'] = msg.handle;
			payload['message'] = JSON.stringify(msg);
			sendMessageToWeb(payload, msg.docId, msg.webId);
		} else if (msg.type == 'delete') {
			//Received request from contact to delete a photo.
			//Notify the corresponding doctor web session.
			var payload = {};
			payload['type'] = 'delete';
			payload['filename'] = msg.filename;
			payload['handle'] = msg.handle;
			payload['message'] = JSON.stringify(msg);
			sendMessageToWeb(payload, msg.docId, msg.webId);
		} else if (msg.type == 'endCall') {
			//Received notification that doctor has finished call.
			//Update the number of available doctors.
			console.log("Received endCall from: " + msg.webId);
			availDocs++;
		} else if (msg.type == 'stats') {
			//Received the stats from a doctor.
			//Update today's statistics.
			console.log("Received stats from: " + msg.webId);
			console.log("Stats: " + msg.stats);
			stats[msg.docId] = JSON.parse(msg.stats);
		}
	});
});
