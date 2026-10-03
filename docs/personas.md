# Personas

These fictional personas define the MVP roles. They do not represent interviews or research with a customer.

## Member: Maya, student project lead

Maya checks supplies before a workshop. She needs to see available quantities, request materials, and check the outcome without sending messages to the manager.

She can browse inventory, submit a purchase request, and view her own requests and their review notes. She cannot approve requests or change quantities. Another member cannot view her request through a guessed URL.

Maya completes her task if she submits a valid request and sees its status after a manager reviews it. On a rejected request, she needs a reason.

## Manager: Jordan, workshop coordinator

Jordan reviews purchases and receives deliveries. They need a record of stock issues and a way to trace quantity changes to an actor and reason.

They can view pending requests, approve or reject them, receive approved purchases, issue stock, and inspect history. They cannot purchase through the member workflow in the MVP.

Jordan completes their task if a receipt updates stock once and creates a matching history record. A failed stock issue must leave quantity unchanged and explain the shortage.

## Portfolio reviewer

The reviewer is an audience for the demo, not an application role. They need setup instructions, synthetic-data disclosure, and evidence for the business rules. The developer supplies demo accounts for the local environment and a short walkthrough.
