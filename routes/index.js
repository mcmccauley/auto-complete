var express = require('express');
var router = express.Router();

/* GET home page. */
router.get('/', function(req, res, next) {
  res.render('index', { title: 'Auto-complete!' });
});

router.get('/get-suggestions', function(req, res, next) {
  var q = req.param('q').toLowerCase();
  client.ZRANGE(q, 0, NUM_SUGGESTIONS_TO_RETURN, function(err, values) {

		// Get the locations and their types in parallel.

		res.json(values);
	})
});



module.exports = router;
