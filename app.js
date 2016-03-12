var express = require('express');
var path = require('path');
var favicon = require('serve-favicon');
var logger = require('morgan');
var cookieParser = require('cookie-parser');
var bodyParser = require('body-parser');
var redis = require("redis");

var fs = require('fs')
var util = require('util')
var stream = require('stream')
var es = require("event-stream");

var app = express();


var config = require('./config.js');


//Create redis client 
client = redis.createClient({
	host: config.REDIS_HOST
});
client.on('connect', function() {
	console.log('connected to redis');
});
client.on("error", function (err) {
	console.log("Redis Error: " + err);
});



app.set('views', path.join(__dirname, 'views'));
app.set('view engine', 'jade');
// uncomment after placing your favicon in /public
//app.use(favicon(path.join(__dirname, 'public', 'favicon.ico')));
app.use(logger('dev'));
app.disable('etag')
// app.use(bodyParser.json());
// app.use(bodyParser.urlencoded({ extended: false }));
// app.use(cookieParser());



var router = express.Router();


router.get('/get-suggestions', function(req, res, next) {
	var q = req.param('q');
	var key = q.toLowerCase();

	var firstChar = q.substring(0,1)
	var secondChar = q.substring(1,2)

	client.ZRANGE(key, 0, config.NUM_SUGGESTIONS_TO_RETURN, function(err, values) {

		// Get the locations and their types in parallel.

		for (var i = values.length - 1; i >= 0; i--) {
			
			if (firstChar && secondChar && firstChar == firstChar.toUpperCase() && secondChar == secondChar.toUpperCase()){
				// If the second letter is uppercase, they probably want the whole thing to be uppercase.
				values[i] = values[i].toUpperCase();
			}
			else {
				// Otherwise, keep the same casing as the input.
				values[i] = values[i].replace(key, q);
			}
		}

		res.json(values);
	})
});
app.use('/', router);
app.use(express.static(path.join(__dirname, 'public')));



// read file into redis
function initMap(fileName) {
client.FLUSHALL(function(){

	var lineNr = 1;

	var batch = client.batch()

	PREFIX_OPTIMIZATION_THRESHOLD = 5;
	var prefixCounts = {}

	s = fs.createReadStream(fileName)
	.pipe(es.split())
	.pipe(
		es.mapSync(function(line){

			// pause the readstream
			s.pause();

			lineNr += 1;

			var parts = line.split(" ");
			var word = parts[0];
			var freq = parts[1];

			if (freq < config.MINIMUM_ALLOWABLE_FREQUENCY){
				batch.exec(function(err, replices){
					if (err) throw err;
					s.resume()
					s.end()
				})
				return;
			}

			for (var c = word.length - 1; c >= 0; c--)
			{
				var substring = word.substring(0, c)
				if (c <= PREFIX_OPTIMIZATION_THRESHOLD){
					if (!prefixCounts[substring]){
						prefixCounts[substring] = 1
					}
					else if (prefixCounts[substring] > config.NUM_SUGGESTIONS_TO_RETURN){
						//console.log(substring, prefixCounts[substring])
						break;
					}
					else
					{
						prefixCounts[substring]++;
					}
				}

				batch.ZADD(substring, -freq, word)
				// batch.ZREMRANGEBYRANK(substring, -(config.NUM_SUGGESTIONS_TO_RETURN + 1), 0)
			}

			if(lineNr % 500 == 0){
				if (lineNr % 5000 == 0)
					console.log('' + lineNr + ': ' + line);
	
				batch.exec(function(err, replies){
					if (err) throw err;	
					s.resume();
				})
			} 
			else
			{
				s.resume();
			}
			
		})
		.on('error', function(){
			console.log('Error while reading file.');
		})
		.on('end', function(){
			console.log('Finished indexing file. Indexed lines: ' + lineNr)
			
			return;
			console.log('Beginning cleanup...')

			var cursor = '0';

			function scan() {
				client.SCAN(
					cursor,
					'COUNT', '50000',
					function(err, res) {
						if (err) throw err;


						// Update the cursor position for the next scan
						cursor = res[0];
						// get the SCAN result for this iteration
						var keys = res[1];
						console.log('cleaning up ' + keys.length + ' keys');

						if (keys.length > 0) {
							for (var i = keys.length - 1; i >= 0; i--) {
								batch.ZREMRANGEBYRANK(keys[i], config.NUM_SUGGESTIONS_TO_RETURN + 1, 0)
							};
						}
						batch.exec()

						if (cursor === '0') {
							return console.log('Cleanup complete');
						}

						return scan();
					}
				);
			}

			scan();
		})
	);
});
}

 initMap(config.DATA_PATH)






// catch 404 and forward to error handler
app.use(function(req, res, next) {
	var err = new Error('Not Found');
	err.status = 404;
	next(err);
});


// error handlers

// development error handler
// will print stacktrace
if (app.get('env') === 'development') {
	app.use(function(err, req, res, next) {
		res.status(err.status || 500);
		res.render('error', {
			message: err.message,
			error: err
		});
	});
}

// production error handler
// no stacktraces leaked to user
app.use(function(err, req, res, next) {
	res.status(err.status || 500);
	res.render('error', {
		message: err.message,
		error: {}
	});
});


app.listen(3000)


module.exports = app;
