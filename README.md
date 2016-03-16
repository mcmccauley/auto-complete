# auto-complete

A project to provide high-performance (sub-millisecond) word suggestions.

By Andrew Scott, Maria McCauley, and Mary Floyd.

# Instructions

## Obtaining the data

* Go to http://kopiwiki.dsd.sztaki.hu/ 
* Download the latest version of the English dump
	* This will take approx. 6 hours due to the speed of the host. Nobody else was seeding the torrent when I downloaded it.
* Extract all the archives using your favorite archiving tool.


## Generating the input

* Install Visual Studio 2013+
* If you aren't running on Windows, you can try using Mono or Roslyn, but it has not been tested with these compilers.
* Open the solution in `WordCount`
* Edit the `Path` constant at the top of the file to point to the directory that contains your input files
	* Each input file must contain the string `wiki` in the file name. Edit the code if yours do not
	* Each input file contains regular old English text. Sentences, paragraphs, whatever. 
* Run the program through Visual Studio.


## Running the website 

* Install Node.js v4.4+ 
* Install Redis v2.8+
* Run `npm install` in the project's root directory to install dependencies.
* Copy `config.example.js` to `config.js`
	* Modify DATA_PATH in the configuration file so that it points to your input file.
	* Each line of the input file contains a word followed by a space followed by a number representing a frequency.
	* The lines in the input file **must** be sorted in descending order by the frequency.
* Run `redis-server` to start Redis. Keep it running and open another command prompt.
* Run `node app.js` to run the app.
* Open your browser and go to `localhost:3000`
