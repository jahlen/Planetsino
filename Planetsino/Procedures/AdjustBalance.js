function adjustBalance(id, amount) {
    var collection = getContext().getCollection();
    var response = getContext().getResponse();

    var query = "SELECT * FROM c WHERE c.id = '" + id + "'";

    // Query documents and take 1st item.
    var isAccepted = collection.queryDocuments(
        collection.getSelfLink(),
        query,
    function (err, feed, options) {
        if (err) throw err;

        // Check the feed and if empty, set the body to 'no docs found', 
        // else take 1st element from feed
        if (!feed || feed.length == 0) {
            response.setBody("Document '" + id + "' not found. Amount = " + amount.toString());
        }
        else {
            feed[0].balance += amount;
            collection.replaceDocument(feed[0]._self, feed[0]);
            response.setBody(JSON.stringify(feed[0]));
        }
    });

    if (!isAccepted) throw new Error('The query was not accepted by the server.');
}
