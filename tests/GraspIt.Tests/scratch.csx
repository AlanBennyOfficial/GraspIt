using System;
using System.Xml.Linq;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        var doc = XDocument.Load(@"C:\GitHub\.NET-Project\tests\GraspIt.Tests\TestResults\95121bf8-12b8-4bbd-b500-13948c25c846\coverage.cobertura.xml");
        var packages = doc.Descendants("package");
        foreach(var pkg in packages)
        {
            var name = pkg.Attribute("name")?.Value;
            var lineRate = pkg.Attribute("line-rate")?.Value;
            Console.WriteLine($"Package: {name}, Line Rate: {lineRate}");
            
            if (name == "GraspIt.Core" || name == "GraspIt.Application")
            {
                var classes = pkg.Descendants("class");
                foreach(var cls in classes)
                {
                    var clsLineRate = cls.Attribute("line-rate")?.Value;
                    if (clsLineRate != "1")
                    {
                        Console.WriteLine($"  Class {cls.Attribute("name")?.Value} line rate: {clsLineRate}");
                    }
                }
            }
        }
    }
}
