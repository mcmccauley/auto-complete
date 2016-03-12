using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WordCount
{
    class Program
    {
        const string Path = "D:/unzipped";

        static List<FileInfo> filesToProcess;

        public static BlockingCollection<string> inputPool;
        public static BlockingCollection<List<KeyValuePair<string, int>>> outputToBeMergedPool;

        static void Main(string[] args)
        {
            var dirInfo = new DirectoryInfo(Path);

            filesToProcess = dirInfo
                .EnumerateFiles()
                .Where(fi => fi.Name.Contains("wiki"))
                //.Take(50)
                .ToList();

            var doneEvent = new ManualResetEvent(false);
            var jobs = new List<WordCount>();

            // Record the total number of files that will be processed so that we know when we will be done.
            WordCount.NumberOfTasks = filesToProcess.Count;

            // Make a queue that contains all the files to be processed that each worker will pull from as needed.
            var inputFiles = new ConcurrentQueue<FileInfo>(filesToProcess);
            
            // Make our producer/consumer pools.
            // Input pool contains the text contents of each file that has been read in.
            inputPool = new BlockingCollection<string>(15);

            // This pool contains a list of words and thier counts that are awaiting merging into the
            // final count.
            outputToBeMergedPool = new BlockingCollection<List<KeyValuePair<string, int>>>(20);


            // Start off our input file worker.
            Task.Run(() =>
            {
                FileInfo fileInfo;
                while (inputFiles.TryDequeue(out fileInfo))
                {
                    StreamReader reader = new StreamReader(new FileStream(fileInfo.FullName, FileMode.Open));

                    string contents = reader.ReadToEnd();

                    // Blocks if the input pool is full until there is room.
                    inputPool.Add(contents);
                }

                // Let consumers know we are done.
                inputPool.CompleteAdding();
            });


            // Start off our merger worker
            var mergedCounts = new Dictionary<string, int>();
            Task.Run(() =>
            {
                for (int i = 0; i < filesToProcess.Count; i++)
                {
                    // Blocks until there is something in outputToBeMergedPool
                    var counts = outputToBeMergedPool.Take();
                    foreach (var grouping in counts)
                    {
                        var key = grouping.Key;
                        if (mergedCounts.ContainsKey(key))
                        {
                            mergedCounts[key] += grouping.Value;
                        }
                        else
                        {
                            mergedCounts[key] = grouping.Value;
                        }
                    }
                }

                // Notify the main thread that all merging is complete.
                doneEvent.Set();
            });


            // Configure and start threads using ThreadPool.
            // Queue up a worker for each input file that we have.
            Console.WriteLine("launching {0} tasks...", filesToProcess.Count);
            foreach (var fileInfo in filesToProcess)
            {
                var wc = new WordCount(fileInfo);
                jobs.Add(wc);
                ThreadPool.QueueUserWorkItem(wc.ThreadPoolCallback);
            }


            // Wait for all threads in pool to calculate and be merged.
            doneEvent.WaitOne();
            Console.WriteLine("All calculations are complete.");
            Console.WriteLine("Writing output...");
            
            // Filter out some of the words that really aren't words.
            // It is faster to do this as the final step than it is to do
            // this inside each worker thread.
            var mergedCountsFiltered = 
                mergedCounts.Where(grouping =>
                    // Filter out words that are one character, except "a" and "i".
                    (grouping.Key.Length > 1 || grouping.Key == "a" || grouping.Key == "i") &&
                    // Filter out words that contain anything that isn't a letter, a hyphen, or an apostrophe
                    !grouping.Key.Any(c => !char.IsLetter(c) && c != '-' && c != '\'') &&
                    // Filter out words that start with a hyphen.
                    !grouping.Key.StartsWith("-") &&
                    // Filter out words end with a hyphen.
                    !grouping.Key.EndsWith("-"))
                .OrderByDescending(kvp => kvp.Value)
                .ToList();

            // For each word count that we have, write out a line to our output file
            // that contains that word and the frequency.
            var outFile = new StreamWriter(new FileStream(filesToProcess.First().DirectoryName + "/out.txt", FileMode.Create));
            foreach (var grouping in mergedCountsFiltered)
            {
                outFile.WriteLine(grouping.Key + " " + grouping.Value);
            }
            outFile.Close();


            outFile = new StreamWriter(new FileStream(filesToProcess.First().DirectoryName + "/frequencyDist.txt", FileMode.Create));
            foreach (var grouping in mergedCountsFiltered.GroupBy(kvp => kvp.Value))
            {
                outFile.WriteLine(grouping.Key + ", " + grouping.Count());
            }
            outFile.Close();
        }
    }

    public class WordCount
    {
        public static int NumberOfTasks;
        private static long numCompleted;

        private static readonly object doneLock = new object();
        private static readonly Stopwatch stopwatch = new Stopwatch();

        public FileInfo FileInfo { get; private set; }

        // Constructor.
        public WordCount(FileInfo fileInfo)
        {
            FileInfo = fileInfo;

            if (!stopwatch.IsRunning)
                stopwatch.Start();
        }

        // Wrapper method for use with thread pool.
        public void ThreadPoolCallback(Object threadContext)
        {
            Console.WriteLine("{0} started...", FileInfo.Name);

            CountWords();

            lock (doneLock)
            {
                numCompleted++;

                NumberOfTasks--;
                if (NumberOfTasks == 0)
                {
                    // All workers are. Tell the merger pool that we're done adding to it.
                    Program.outputToBeMergedPool.CompleteAdding();
                }

                Console.WriteLine("Average time per file: {0:F2}", ((double)stopwatch.ElapsedMilliseconds / 1000) / numCompleted);
            }
        }

        public void CountWords()
        {
            Console.WriteLine("input has {0}", Program.inputPool.Count);
            string contents = Program.inputPool.Take();

            // Split the file contents on newlines and spaces.
            var words = contents.Split(' ', '\r', '\n');

            // Count up all the words in the input.
            // Keep all our data as lowercase, and trim off any punctuation from the ends of each word.
            var counts = words
                .GroupBy(s => s
                                .ToLower()
                                .Trim('"', '\'', '\t', '.', ',', ';', ':', '[', ']', '(', ')', '{', '}', '*', '='))
                .Select(grouping => new KeyValuePair<string, int>(grouping.Key, grouping.Count()))
                .ToList();

            Console.WriteLine("output has {0}", Program.outputToBeMergedPool.Count);
            Program.outputToBeMergedPool.Add(counts);
        }
    }
}
