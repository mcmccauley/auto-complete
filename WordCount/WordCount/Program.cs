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
                .Where(fi => !fi.Name.Contains("out"))
                //.Take(10)
                .ToList();

            var doneEvent = new ManualResetEvent(false);
            var jobs = new List<WordCount>();

            WordCount.NumberOfTasks = filesToProcess.Count;



            // A bounded collection. It can hold no more 
            // than 100 items at once.
            var inputFiles = new ConcurrentQueue<FileInfo>(filesToProcess);
            
            inputPool = new BlockingCollection<string>(15);
            outputToBeMergedPool = new BlockingCollection<List<KeyValuePair<string, int>>>(20);

            Task.Run(() =>
            {
                FileInfo fileInfo;
                while (inputFiles.TryDequeue(out fileInfo))
                {
                    StreamReader reader = new StreamReader(new FileStream(fileInfo.FullName, FileMode.Open));

                    string contents = reader.ReadToEnd();

                    // Blocks if numbers.Count == dataItems.BoundedCapacity
                    inputPool.Add(contents);
                }

                // Let consumer know we are done.
                inputPool.CompleteAdding();
            });

            // The merger thread
            var mergedCounts = new Dictionary<string, int>();
            Task.Run(() =>
            {
                for (int i = 0; i < filesToProcess.Count; i++)
                {
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

                doneEvent.Set();
            });


            // Configure and start threads using ThreadPool.
            Console.WriteLine("launching {0} tasks...", filesToProcess.Count);
            //ThreadPool.SetMinThreads(16, 16);
            foreach (var fileInfo in filesToProcess)
            {
                var wc = new WordCount(fileInfo);
                jobs.Add(wc);
                ThreadPool.QueueUserWorkItem(wc.ThreadPoolCallback);
            }


            // Wait for all threads in pool to calculate.
            doneEvent.WaitOne();
            Console.WriteLine("All calculations are complete.");
            

            var mergedCountsFiltered = mergedCounts
                                        .Where(grouping =>
                                                    (grouping.Key.Length > 1 || grouping.Key == "a" || grouping.Key == "i") &&
                                                   !grouping.Key.Any(c => !char.IsLetter(c) && c != '-' && c != '\'') &&
                                                   !grouping.Key.StartsWith("-") &&
                                                   !grouping.Key.EndsWith("-"))
                                        .OrderByDescending(kvp => kvp.Value);


            var outFile = new StreamWriter(new FileStream(filesToProcess.First().DirectoryName + "/out.txt", FileMode.Create));
            foreach (var grouping in mergedCountsFiltered)
            {
                outFile.WriteLine(grouping.Key + " " + grouping.Value);
            }
            outFile.Close();
        }
    }

    public class WordCount
    {
        public static int NumberOfTasks;
        private static readonly object doneLock = new object();

        private static long totalTime;
        private static long numCompleted;

        Stopwatch stopwatch = new Stopwatch();

        public FileInfo FileInfo { get; private set; }

        // Constructor.
        public WordCount(FileInfo fileInfo)
        {
            FileInfo = fileInfo;

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
                    Program.outputToBeMergedPool.CompleteAdding();
                }

                Console.WriteLine("Average time per file: {0:F2}", ((double)stopwatch.ElapsedMilliseconds / 1000) / numCompleted);
            }

        }


        // Recursive method that calculates the Nth Fibonacci number.
        public void CountWords()
        {
            Console.WriteLine("input has {0}", Program.inputPool.Count);
            string contents = Program.inputPool.Take();

            var words = contents.Split(' ', '\r', '\n');

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
