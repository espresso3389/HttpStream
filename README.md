HttpStream
==========

This C# PCL project implements randomly accessible stream on HTTP 1.1 transport.
Typically, HttpClient provides almost all HTTP features but it just provides us with a way to download a file completely.
The implementation take advantage of HTTP 1.1 range access.
