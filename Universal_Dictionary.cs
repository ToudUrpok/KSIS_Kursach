using System.Collections.Generic;

namespace ProxyServer
{
    public class Universal_Dictionary
    {
        private Dictionary<int, object> Dictionary;
        private int ElementsCount;
        
        public Universal_Dictionary()
        {
            Dictionary = new Dictionary<int, object>();
            ElementsCount = 0;
        }

        public int AddElement(object element)
        {
            int key = ElementsCount;
            Dictionary.Add(key, element);
            try
            {
                ElementsCount = checked(ElementsCount + 1);
            }
            catch (System.OverflowException)
            {
                ElementsCount = 0;
            }
            return key;
        }

        public void RemoveElement(int key)
        {
            Dictionary.Remove(key);
        }

        public object GetElement(int key)
        {
            return Dictionary.ContainsKey(key) ? Dictionary[key] : null;
        }
    }
}
