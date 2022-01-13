namespace LPS.Client
{
    public class Program
    {
        private static Random random = new Random();

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        
        public static void Main(string[] args)
        {
            var client = new Client("52.175.74.209", 11001);
            client.Start();

            var input = Console.ReadLine();

            while (input != null)
            {
                client.Send(input);
                input = Console.ReadLine();
            }
        }
    }
}
