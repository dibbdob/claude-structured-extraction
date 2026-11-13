# The labelled tickets

`tickets.jsonl` holds thirty support emails and the extraction a careful person would make from each. They
were written by hand for this repository: none is a real customer's email, and the labels are one
person's judgement, not ground truth.

They are chosen for the cases that are easy to get wrong, not for being typical:

- an outage reported in German (t16), and one where a suspended account means the customer cannot work
  although nothing is broken (t20);
- frustration that is not an outage (t07 is, t18 and t21 are not), and sarcasm (t21);
- legal, security and privacy requests that must reach a person (t09, t10, t22, t25);
- an email that tries to instruct the extractor (t11), which must be read as data, not followed;
- a message with no request in it (t28).

`summary` is not labelled: it is checked for being present and short, not for its wording.

Thirty is enough to find a gross failure and too few to separate two good configurations by a point or
two of accuracy. The eval report says so beside each score.
