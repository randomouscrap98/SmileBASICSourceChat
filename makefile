comp=mcs
run=mono
files=main.cs http.cs chat.cs auth.cs string.cs user.cs\
		logger.cs GeneralExtensions.cs

chat.exe: $(files) 
	$(comp) -r:websocket-sharp.dll -r:Newtonsoft.Json.dll -out:chat.exe $(files)

.PHONY: run
run: chat.exe
	$(run) chat.exe

.PHONY: runserver
runserver: chat.exe
	mkdir -p server
	cp chat.exe *.dll server/
	$(run) server/chat.exe

.PHONY: clean
clean:
	rm -f *.exe
