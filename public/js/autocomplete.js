var app = angular.module('autocomplete', ['ui.bootstrap']);

app.controller('auto', function($scope, $http){

	var phrases = [
		"Start typing to get suggestions!",
		"Mew!",
		"Meow!",
		"What do you want to say?",
		"What's up?",
		"Spit it out!",
		"Purr purr purr...",
		"¡Vamos a tener una fiesta!",
		" You talkin' to me?",
	]

	$scope.phrase = phrases[Math.floor((Math.random() * phrases.length))]; 

	// Any function returning a promise object can be used to load values asynchronously. 
	$scope.getSuggestions = function(val) {

		var words = val.split(" ");
		var lastWord = words[words.length - 1];
	
		return $http.get('/get-suggestions', {
			params: {
				q: lastWord
			}	
		}).then(function(resp) {
			return resp.data;
		});


	};

	$scope.onSelect = function(item, model) {
		var viewValue = $scope.form.input.$viewValue;

		var words = viewValue.split(" ");
		words[words.length - 1] = model + " ";
		var content = words.join(" ");
		$scope.text = content;
	}
});