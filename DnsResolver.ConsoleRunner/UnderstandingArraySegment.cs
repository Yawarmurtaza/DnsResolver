using System;

namespace DnsResolver.ConsoleRunner
{
    public class UnderstandingArraySegment
    {
        public void Run()
        {
            byte[] arr = {1, 2, 3, 4, 5, 6, 7, 8};
            var segment = new ArraySegment<byte>(arr, 0, 1);
            
            
            foreach (int i in segment)
            {
                Console.WriteLine(i);
            }

        }
    }
}