extern void __VERIFIER_error() __attribute__ ((__noreturn__));

#include <pthread.h>

#define N 5

int data = 0;

void *t1(void *arg) {
  for(int i = 0; i < N; i++)
  {
    data++;
  }

  return 0;
}

void *t2(void *arg) {
  data++;
  return 0;
}

int main() {
  pthread_mutex_t lock;
  pthread_mutex_init(&lock, 0);

  pthread_t tid1, tid2;

  pthread_create(&tid1, 0, t1, 0);
  pthread_create(&tid2, 0, t2, 0);

  pthread_join(tid1, 0);
  pthread_join(tid2, 0);

  return 0;
}
