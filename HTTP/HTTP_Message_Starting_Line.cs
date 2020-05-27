using System;

namespace ProxyServer
{
    public class HTTP_Message_Starting_Line
    {
        public string InitialPart; //для запросов - method, для ответов - httpVersion
        public string MiddlePart; //для запросов - URI, для ответов - StatusCode
        public string EndingPart; //для запросов - httpVersion, для ответов - reasonPhrase(если есть*) 

        public HTTP_Message_Starting_Line(string initialPart, string middlePart, string endingPart)
        {
            InitialPart = initialPart;
            MiddlePart = middlePart;
            EndingPart = endingPart;
        }
    }
}
