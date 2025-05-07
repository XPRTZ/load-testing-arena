from locust import HttpUser, task, between

class QuickPizzaUser(HttpUser):
    wait_time = between(1, 2)

    @task
    def visit_homepage(self):
        self.client.get("/")
