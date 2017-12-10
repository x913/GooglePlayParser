Google play parser. Usage:

	--query Query to parse.
	--mode Work mode: s - parse suggestions, r - get search results by query, d - parse by developer, c - encode result json to csv

Examples:

	GoogleSuggestionsParser.exe --query "how to draw" --mode r
	It will parse google play by "how to draw" query and save result into two files "how to draw.json" and "how to draw.csv"

	
	GoogleSuggestionsParser.exe --query Riano --mode d
	It will parse all applications from choosen developer and save them to json and csv files


	GoogleSuggestionsParser.exe --query "how to draw" --mode s
	It will parse all suggesions adding letters them, i.e. how to draw ac, how to draw ab etc.
	

