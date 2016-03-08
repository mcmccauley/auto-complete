var express = require('express');
var path = require('path');
var favicon = require('serve-favicon');
var logger = require('morgan');
var cookieParser = require('cookie-parser');
var bodyParser = require('body-parser');
var redis = require("redis");

var routes = require('./routes/index');
var users = require('./routes/users');

var app = express();

// view engine setup
app.set('views', path.join(__dirname, 'views'));
app.set('view engine', 'jade');

//Create redis client 
client = redis.createClient();

client.on('connect', function() {
  console.log('connected');
});

client.on("error", function (err) {
  console.log("Redis Error: " + err);
});


// uncomment after placing your favicon in /public
//app.use(favicon(path.join(__dirname, 'public', 'favicon.ico')));
app.use(logger('dev'));
app.use(bodyParser.json());
app.use(bodyParser.urlencoded({ extended: false }));
app.use(cookieParser());
app.use(express.static(path.join(__dirname, 'public')));

app.use('/', routes);
app.use('/users', users);

// catch 404 and forward to error handler
app.use(function(req, res, next) {
  var err = new Error('Not Found');
  err.status = 404;
  next(err);
});



// read file into redis


  var fs = require('fs')
    , util = require('util')
    , stream = require('stream')
    , es = require("event-stream");


function initMap(fileName) {

  var lineNr = 1;

  s = fs.createReadStream(fileName)
      .pipe(es.split())
      .pipe(es.mapSync(function(line){

          // pause the readstream
          s.pause();

          lineNr += 1;

          // (function(){

              var parts = line.split(" ");
              var word = parts[0];
              var freq = parts[1];
              // process line here and call s.resume() when rdy
              console.log(line);

              for (var c = 1; c <= word.length; c++)
              {
                client.ZADD(word.substring(0, c), 10000000000000 - freq, word)
              }
                

              // resume the readstream
              s.resume();

          // })();
          })
          .on('error', function(){
              console.log('Error while reading file.');
          })
          .on('end', function(){
              console.log('Read entirefile.')
          })
      );
}

 initMap('smallSegment.txt')


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


module.exports = app;
