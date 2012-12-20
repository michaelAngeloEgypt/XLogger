XLogger
=======

lightweight logging utility that consists of a single file. 
Just copy the file XLogger.cs in your utility project and it will be available to you.

Example:
````````
- to add a seperator:     XLogger.Sep()
- to log an exception:    XLogger.Error(ex.ToString())    //where ex is the exception object
- to log a custom info:   XLogger.Info()
